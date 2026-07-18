using System.Globalization;
using InventoryErp.Application.Common;
using InventoryErp.Application.DTOs.Quotations;
using InventoryErp.Application.DTOs.Settings;
using InventoryErp.Application.Interfaces;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace InventoryErp.Infrastructure.Documents;

/// <summary>
/// Renders a quotation to PDF with QuestPDF. Lives in Infrastructure because QuestPDF is a
/// third-party rendering concern; the Application layer sees only
/// <see cref="IQuotationPdfService"/>.
/// </summary>
public sealed class QuotationPdfService : IQuotationPdfService
{
    /// <summary>
    /// Maximum logo box in PDF points (1 pt = 1/72 in), so roughly 63 mm × 21 mm.
    /// </summary>
    /// <remarks>
    /// A4 portrait with 40 pt margins leaves ~515 pt of content width. Capping the logo at 180 pt
    /// keeps it to about a third of the line, leaving room for the company block beside it, and
    /// 60 pt of height keeps the header shorter than the first table rows. The image is scaled to
    /// <b>fit inside</b> this box preserving aspect ratio, so a 2000 px wide banner and a tall
    /// square mark both shrink to fit rather than distorting or pushing the layout apart.
    /// </remarks>
    private const float MaxLogoWidthPoints = 180f;

    private const float MaxLogoHeightPoints = 60f;

    /// <summary>Indian numbering for money, matching the rest of the app.</summary>
    private static readonly CultureInfo Money = CultureInfo.GetCultureInfo("en-IN");

    private readonly IQuotationService _quotations;
    private readonly ISettingsService _settings;
    private readonly ILogoStorage _logoStorage;
    private readonly ILogger<QuotationPdfService> _logger;

    public QuotationPdfService(
        IQuotationService quotations,
        ISettingsService settings,
        ILogoStorage logoStorage,
        ILogger<QuotationPdfService> logger)
    {
        _quotations = quotations;
        _settings = settings;
        _logoStorage = logoStorage;
        _logger = logger;
    }

    public async Task<ServiceResult<GeneratedDocument>> GenerateAsync(
        Guid quotationId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var quotation = await _quotations.GetByIdAsync(quotationId, companyId, cancellationToken);

        if (!quotation.IsSuccess)
        {
            return ServiceResult<GeneratedDocument>.NotFound(
                quotation.Error ?? "Quotation not found.");
        }

        var settings = await _settings.GetSettingsAsync(companyId, cancellationToken);

        if (!settings.IsSuccess)
        {
            return ServiceResult<GeneratedDocument>.Failure(
                settings.Error ?? "Company settings could not be read.");
        }

        var logo = await TryLoadLogoAsync(
            settings.Data!.LogoPath, quotation.Data!.QuotationNumber, cancellationToken);

        var bytes = Render(quotation.Data!, settings.Data, logo);

        return ServiceResult<GeneratedDocument>.Success(new GeneratedDocument(
            $"{quotation.Data!.QuotationNumber}.pdf",
            "application/pdf",
            bytes));
    }

    /// <summary>
    /// Loads the logo, degrading to null for any failure whatsoever.
    /// </summary>
    /// <remarks>
    /// <see cref="ILogoStorage.TryReadAsync"/> is contracted to return null rather than throw, but
    /// this catches regardless: a decorative image must never be able to fail a commercial
    /// document. The alternative — a quotation that cannot be sent because a logo file moved — is
    /// far worse than one printed without a logo.
    /// </remarks>
    private async Task<byte[]?> TryLoadLogoAsync(
        string? logoPath,
        string quotationNumber,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(logoPath))
        {
            return null;   // No logo configured; nothing to warn about.
        }

        byte[]? logo = null;

        try
        {
            logo = await _logoStorage.TryReadAsync(logoPath, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Reading logo {LogoPath} threw; continuing without it.", logoPath);
        }

        if (logo is null)
        {
            _logger.LogWarning(
                "Quotation {Number} rendered without its logo: {LogoPath} could not be read.",
                quotationNumber,
                logoPath);
        }

        return logo;
    }

    private byte[] Render(QuotationDto quotation, CompanySettingsDto settings, byte[]? logo)
    {
        var accent = ParseAccent(settings.PrimaryAccentColor);

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(t => t.FontSize(9).FontColor(Colors.Grey.Darken4));

                page.Header().Element(header => ComposeHeader(header, settings, logo, accent));
                page.Content().Element(content => ComposeContent(content, quotation, settings, accent));
                page.Footer().Element(footer => ComposeFooter(footer, settings));
            });
        }).GeneratePdf();
    }

    private static void ComposeHeader(
        IContainer container,
        CompanySettingsDto settings,
        byte[]? logo,
        string accent)
    {
        container.PaddingBottom(12).Column(column =>
        {
            column.Item().Row(row =>
            {
                if (logo is not null)
                {
                    // Constrained box + FitArea: scales down to fit, never distorts, never
                    // enlarges a small logo beyond its natural size.
                    row.ConstantItem(MaxLogoWidthPoints)
                        .MaxHeight(MaxLogoHeightPoints)
                        .AlignLeft()
                        .AlignMiddle()
                        .Image(logo)
                        .FitArea();
                }

                row.RelativeItem().AlignRight().Column(details =>
                {
                    details.Item().Text(settings.Name)
                        .FontSize(15).Bold().FontColor(accent);

                    if (!string.IsNullOrWhiteSpace(settings.Tagline))
                    {
                        details.Item().Text(settings.Tagline).FontSize(8).FontColor(Colors.Grey.Darken1);
                    }

                    foreach (var line in AddressLines(settings))
                    {
                        details.Item().Text(line).FontSize(8);
                    }
                });
            });

            column.Item().PaddingTop(10).LineHorizontal(1.5f).LineColor(accent);
        });
    }

    private static void ComposeContent(
        IContainer container,
        QuotationDto quotation,
        CompanySettingsDto settings,
        string accent)
    {
        container.PaddingTop(14).Column(column =>
        {
            column.Item().Text("QUOTATION").FontSize(18).Bold().FontColor(accent);

            column.Item().PaddingTop(10).Row(row =>
            {
                row.RelativeItem().Column(to =>
                {
                    to.Item().Text("Quotation for").FontSize(7).FontColor(Colors.Grey.Darken1);
                    to.Item().Text(quotation.CustomerName).FontSize(11).SemiBold();
                });

                row.ConstantItem(190).Column(meta =>
                {
                    MetaRow(meta, "Number", quotation.QuotationNumber);
                    MetaRow(meta, "Date", quotation.QuotationDate.ToString("dd MMM yyyy"));
                    MetaRow(
                        meta,
                        "Valid until",
                        quotation.ValidUntil?.ToString("dd MMM yyyy") ?? "No expiry");
                });
            });

            column.Item().PaddingTop(16).Element(table => ComposeLineTable(table, quotation, accent));
            column.Item().PaddingTop(12).Element(totals => ComposeTotals(totals, quotation, accent));

            if (!string.IsNullOrWhiteSpace(quotation.Notes))
            {
                column.Item().PaddingTop(16).Column(notes =>
                {
                    notes.Item().Text("Notes").FontSize(8).SemiBold().FontColor(accent);
                    notes.Item().PaddingTop(2).Text(quotation.Notes).FontSize(8);
                });
            }

            if (!string.IsNullOrWhiteSpace(settings.InvoiceTerms))
            {
                column.Item().PaddingTop(12).Column(terms =>
                {
                    terms.Item().Text("Terms and conditions").FontSize(8).SemiBold().FontColor(accent);
                    terms.Item().PaddingTop(2).Text(settings.InvoiceTerms).FontSize(8);
                });
            }
        });
    }

    private static void ComposeLineTable(IContainer container, QuotationDto quotation, string accent)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(4);   // product
                columns.ConstantColumn(34);  // qty
                columns.ConstantColumn(60);  // unit price
                columns.ConstantColumn(40);  // discount
                columns.ConstantColumn(40);  // gst
                columns.ConstantColumn(60);  // tax
                columns.ConstantColumn(66);  // total
            });

            table.Header(header =>
            {
                HeaderCell(header, "Item", accent, left: true);
                HeaderCell(header, "Qty", accent);
                HeaderCell(header, "Rate", accent);
                HeaderCell(header, "Disc", accent);
                HeaderCell(header, "GST", accent);
                HeaderCell(header, "Tax", accent);
                HeaderCell(header, "Amount", accent);
            });

            foreach (var line in quotation.Lines)
            {
                table.Cell().Element(Body).Column(item =>
                {
                    item.Item().Text(line.ProductName).FontSize(8);
                    item.Item().Text(line.ProductSku).FontSize(6.5f).FontColor(Colors.Grey.Darken1);
                });

                NumberCell(table, line.Quantity.ToString(Money));
                NumberCell(table, line.UnitPrice.ToString("N2", Money));
                NumberCell(table, $"{line.DiscountPercent.ToString("0.##", Money)}%");
                NumberCell(table, $"{line.GstPercent.ToString("0.##", Money)}%");
                NumberCell(table, line.TaxAmount.ToString("N2", Money));
                NumberCell(table, line.TotalAmount.ToString("N2", Money), bold: true);
            }
        });

        static IContainer Body(IContainer c) =>
            c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5);

        static void HeaderCell(TableCellDescriptor header, string text, string accent, bool left = false)
        {
            var cell = header.Cell().BorderBottom(1).BorderColor(accent).PaddingVertical(5);
            var content = cell.Text(text).FontSize(7.5f).SemiBold().FontColor(accent);

            if (!left)
            {
                content.AlignRight();
            }
        }

        static void NumberCell(TableDescriptor table, string text, bool bold = false)
        {
            var cell = table.Cell()
                .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                .PaddingVertical(5)
                .AlignRight()
                .AlignMiddle()
                .Text(text)
                .FontSize(8);

            if (bold)
            {
                cell.SemiBold();
            }
        }
    }

    private static void ComposeTotals(IContainer container, QuotationDto quotation, string accent)
    {
        container.Row(row =>
        {
            row.RelativeItem();

            row.ConstantItem(220).Column(totals =>
            {
                TotalRow(totals, "Subtotal", quotation.SubTotal);
                TotalRow(totals, "Discount", quotation.DiscountAmount);
                TotalRow(totals, "GST", quotation.TaxAmount);

                totals.Item().PaddingTop(4).BorderTop(1).BorderColor(accent).PaddingTop(4).Row(grand =>
                {
                    grand.RelativeItem().Text("Total").FontSize(10).Bold();
                    grand.ConstantItem(90).AlignRight()
                        .Text(quotation.TotalAmount.ToString("N2", Money))
                        .FontSize(12).Bold().FontColor(accent);
                });
            });
        });

        static void TotalRow(ColumnDescriptor column, string label, decimal value)
        {
            column.Item().PaddingVertical(1).Row(row =>
            {
                row.RelativeItem().Text(label).FontSize(8.5f).FontColor(Colors.Grey.Darken2);
                row.ConstantItem(90).AlignRight().Text(value.ToString("N2", Money)).FontSize(8.5f);
            });
        }
    }

    private static void ComposeFooter(IContainer container, CompanySettingsDto settings)
    {
        container.BorderTop(0.5f).BorderColor(Colors.Grey.Lighten1).PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text(settings.InvoiceFooter)
                .FontSize(7).FontColor(Colors.Grey.Darken1);

            row.ConstantItem(80).AlignRight().Text(text =>
            {
                text.DefaultTextStyle(t => t.FontSize(7).FontColor(Colors.Grey.Darken1));
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        });
    }

    private static void MetaRow(ColumnDescriptor column, string label, string value)
    {
        column.Item().PaddingVertical(1).Row(row =>
        {
            row.RelativeItem().Text(label).FontSize(7.5f).FontColor(Colors.Grey.Darken1);
            row.ConstantItem(110).AlignRight().Text(value).FontSize(8.5f).SemiBold();
        });
    }

    private static IEnumerable<string> AddressLines(CompanySettingsDto s)
    {
        if (!string.IsNullOrWhiteSpace(s.AddressLine))
        {
            yield return s.AddressLine;
        }

        var locality = string.Join(", ", new[] { s.City, s.State, s.PinCode }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

        if (locality.Length > 0)
        {
            yield return locality;
        }

        if (!string.IsNullOrWhiteSpace(s.Country))
        {
            yield return s.Country;
        }

        var contact = string.Join("  ·  ", new[] { s.Mobile, s.Email, s.Website }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

        if (contact.Length > 0)
        {
            yield return contact;
        }

        var tax = string.Join("  ·  ", new[]
        {
            string.IsNullOrWhiteSpace(s.Gstin) ? null : $"GSTIN: {s.Gstin}",
            string.IsNullOrWhiteSpace(s.Pan) ? null : $"PAN: {s.Pan}",
        }.Where(part => part is not null));

        if (tax.Length > 0)
        {
            yield return tax;
        }
    }

    /// <summary>Falls back to the default rather than failing on a malformed stored colour.</summary>
    private static string ParseAccent(string? value)
        => !string.IsNullOrWhiteSpace(value) && value.StartsWith('#') && (value.Length is 4 or 7)
            ? value
            : CompanySettingsDto.DefaultAccentColor;
}
