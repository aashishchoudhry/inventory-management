using InventoryErp.Application.Common;

namespace InventoryErp.Application.Interfaces;

public interface IQuotationPdfService
{
    /// <summary>
    /// Renders a quotation to a PDF.
    /// </summary>
    /// <returns>
    /// <c>NotFound</c> when the quotation does not exist in the company; otherwise
    /// <c>Success</c> with the document bytes.
    /// </returns>
    /// <remarks>
    /// A missing or unreadable logo is <b>never</b> a failure — the header renders without it.
    /// </remarks>
    Task<ServiceResult<GeneratedDocument>> GenerateAsync(
        Guid quotationId,
        Guid companyId,
        CancellationToken cancellationToken = default);
}

/// <summary>A rendered document ready to be returned to a browser.</summary>
public sealed record GeneratedDocument(string FileName, string ContentType, byte[] Content);
