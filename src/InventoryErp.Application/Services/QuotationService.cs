using InventoryErp.Application.Common;
using InventoryErp.Application.DTOs.Quotations;
using InventoryErp.Application.Interfaces;
using InventoryErp.Domain.Common;
using InventoryErp.Domain.Entities;
using InventoryErp.Domain.Interfaces;

namespace InventoryErp.Application.Services;

/// <summary>
/// Application service for quotations. Depends only on <see cref="IUnitOfWork"/> — no EF Core
/// type reaches this layer.
/// </summary>
public sealed class QuotationService : IQuotationService
{
    private const string NumberPrefix = "QT";
    private const int SequenceDigits = 4;
    private const int MaxPageSize = 200;

    /// <summary>Upper bound when loading the lines of one quotation.</summary>
    private const int MaxLinesPerQuotation = 500;

    private readonly IUnitOfWork _unitOfWork;

    public QuotationService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    private IRepository<Quotation> Quotations => _unitOfWork.Repository<Quotation>();
    private IRepository<QuotationLine> Lines => _unitOfWork.Repository<QuotationLine>();
    private IRepository<Customer> Customers => _unitOfWork.Repository<Customer>();
    private IRepository<Product> Products => _unitOfWork.Repository<Product>();

    // ------------------------------------------------------------------ create

    public async Task<ServiceResult<QuotationDto>> CreateQuotationAsync(
        Guid companyId,
        CreateQuotationRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = ValidateShape(request);

        if (companyId == Guid.Empty)
        {
            errors.Insert(0, "A company must be specified.");
        }

        if (errors.Count > 0)
        {
            return ServiceResult<QuotationDto>.Invalid(errors);
        }

        // Existence checks are scoped to the company. A customer or product belonging to another
        // tenant must read as "does not exist", not as a usable reference.
        var customer = await Customers.GetByIdAsync(request.CustomerId, cancellationToken);

        if (customer is null || customer.CompanyId != companyId)
        {
            return ServiceResult<QuotationDto>.NotFound(
                $"No customer with id '{request.CustomerId}' in this company.");
        }

        var productIds = request.Lines.Select(l => l.ProductId).Distinct().ToList();

        // One query for every referenced product, rather than one per line.
        var products = (await Products.ListPagedAsync(
                predicate: p => productIds.Contains(p.Id) && p.CompanyId == companyId,
                orderBy: p => p.Name,
                pageNumber: 1,
                pageSize: Math.Max(productIds.Count, 1),
                cancellationToken: cancellationToken))
            .Items
            .ToDictionary(p => p.Id);

        var missing = productIds.FirstOrDefault(id => !products.ContainsKey(id));

        if (missing != Guid.Empty)
        {
            return ServiceResult<QuotationDto>.NotFound(
                $"No product with id '{missing}' in this company.");
        }

        var quotationNumber = await GenerateQuotationNumberAsync(
            companyId, request.QuotationDate, cancellationToken);

        if (quotationNumber is null)
        {
            return ServiceResult<QuotationDto>.Conflict(
                "Could not allocate a unique quotation number. Please retry.");
        }

        // The id is assigned client-side, so lines can reference their parent before either is
        // saved — the whole graph is written in one SaveChanges.
        var quotation = new Quotation
        {
            CompanyId = companyId,
            QuotationNumber = quotationNumber,
            CustomerId = request.CustomerId,
            QuotationDate = request.QuotationDate,
            ValidUntil = request.ValidUntil,
            Notes = request.Notes,
        };

        var lineEntities = new List<QuotationLine>(request.Lines.Count);
        var lineDtos = new List<QuotationLineDto>(request.Lines.Count);

        decimal subTotal = 0m, discountTotal = 0m, taxTotal = 0m;

        foreach (var line in request.Lines)
        {
            var amounts = QuotationCalculator.CalculateLine(
                line.Quantity, line.UnitPrice, line.DiscountPercent, line.GstPercent);

            var entity = new QuotationLine
            {
                QuotationId = quotation.Id,
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                DiscountPercent = line.DiscountPercent,
                GstPercent = line.GstPercent,
                TaxAmount = amounts.Tax,
                TotalAmount = amounts.Total,
            };

            lineEntities.Add(entity);

            subTotal += amounts.Gross;
            discountTotal += amounts.Discount;
            taxTotal += amounts.Tax;

            var product = products[line.ProductId];
            lineDtos.Add(ToLineDto(entity, amounts, product.Name, product.Sku));
        }

        // Header totals are sums of already-rounded line figures, so the header always reconciles
        // exactly with the sum of its lines.
        quotation.SubTotal = subTotal;
        quotation.DiscountAmount = discountTotal;
        quotation.TaxAmount = taxTotal;
        quotation.TotalAmount = subTotal - discountTotal + taxTotal;

        await Quotations.AddAsync(quotation, cancellationToken);

        foreach (var entity in lineEntities)
        {
            await Lines.AddAsync(entity, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<QuotationDto>.Success(ToDto(quotation, customer.Name, lineDtos));
    }

    // -------------------------------------------------------------------- read

    public Task<ServiceResult<PagedResult<QuotationListItemDto>>> GetAllAsync(
        Guid companyId,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
        => SearchAsync(companyId, keyword: null, pageNumber, pageSize, cancellationToken);

    public async Task<ServiceResult<PagedResult<QuotationListItemDto>>> SearchAsync(
        Guid companyId,
        string? keyword,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var errors = ValidatePaging(companyId, pageNumber, pageSize);

        if (errors.Count > 0)
        {
            return ServiceResult<PagedResult<QuotationListItemDto>>.Invalid(errors);
        }

        var term = keyword?.Trim().ToLowerInvariant();

        System.Linq.Expressions.Expression<Func<Quotation, bool>> predicate =
            string.IsNullOrWhiteSpace(term)
                ? q => q.CompanyId == companyId
                : q => q.CompanyId == companyId && q.QuotationNumber.ToLower().Contains(term);

        // Newest first: a quotation list is a work queue, not a reference table.
        var page = await Quotations.ListPagedAsync(
            predicate,
            orderBy: q => q.QuotationDate,
            pageNumber,
            pageSize,
            descending: true,
            cancellationToken);

        if (page.Items.Count == 0)
        {
            return ServiceResult<PagedResult<QuotationListItemDto>>.Success(
                page.Map(_ => new QuotationListItemDto()));
        }

        var customerNames = await LoadCustomerNamesAsync(
            page.Items.Select(q => q.CustomerId).Distinct().ToList(), cancellationToken);

        var quotationIds = page.Items.Select(q => q.Id).ToList();

        // One query for the line counts of every quotation on this page.
        var lineCounts = (await Lines.ListPagedAsync(
                predicate: l => quotationIds.Contains(l.QuotationId),
                orderBy: l => l.QuotationId,
                pageNumber: 1,
                pageSize: MaxLinesPerQuotation * Math.Max(quotationIds.Count, 1),
                cancellationToken: cancellationToken))
            .Items
            .GroupBy(l => l.QuotationId)
            .ToDictionary(g => g.Key, g => g.Count());

        return ServiceResult<PagedResult<QuotationListItemDto>>.Success(page.Map(q =>
            new QuotationListItemDto
            {
                Id = q.Id,
                QuotationNumber = q.QuotationNumber,
                CustomerName = customerNames.GetValueOrDefault(q.CustomerId, "(unknown customer)"),
                QuotationDate = q.QuotationDate,
                ValidUntil = q.ValidUntil,
                TotalAmount = q.TotalAmount,
                LineCount = lineCounts.GetValueOrDefault(q.Id, 0),
            }));
    }

    public async Task<ServiceResult<QuotationDto>> GetByIdAsync(
        Guid id,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var quotation = await Quotations.GetByIdAsync(id, cancellationToken);

        // Scoped to the company: another tenant's quotation reads as not found, not forbidden,
        // so an id cannot be probed for existence.
        if (quotation is null || quotation.CompanyId != companyId)
        {
            return ServiceResult<QuotationDto>.NotFound($"No quotation with id '{id}'.");
        }

        var customer = await Customers.GetByIdAsync(quotation.CustomerId, cancellationToken);

        var lines = (await Lines.ListPagedAsync(
                predicate: l => l.QuotationId == quotation.Id,
                orderBy: l => l.CreatedAtUtc,
                pageNumber: 1,
                pageSize: MaxLinesPerQuotation,
                cancellationToken: cancellationToken))
            .Items;

        var productIds = lines.Select(l => l.ProductId).Distinct().ToList();

        var products = productIds.Count == 0
            ? []
            : (await Products.ListPagedAsync(
                    predicate: p => productIds.Contains(p.Id),
                    orderBy: p => p.Name,
                    pageNumber: 1,
                    pageSize: productIds.Count,
                    cancellationToken: cancellationToken))
                .Items
                .ToDictionary(p => p.Id);

        var lineDtos = lines.Select(l =>
        {
            // Recomputed for display only — gross and discount are not persisted on the line.
            var amounts = QuotationCalculator.CalculateLine(
                l.Quantity, l.UnitPrice, l.DiscountPercent, l.GstPercent);

            var product = products.GetValueOrDefault(l.ProductId);
            return ToLineDto(l, amounts, product?.Name ?? "(deleted product)", product?.Sku ?? "—");
        }).ToList();

        return ServiceResult<QuotationDto>.Success(
            ToDto(quotation, customer?.Name ?? "(unknown customer)", lineDtos));
    }

    private async Task<Dictionary<Guid, string>> LoadCustomerNamesAsync(
        List<Guid> customerIds,
        CancellationToken cancellationToken)
    {
        if (customerIds.Count == 0)
        {
            return [];
        }

        var customers = await Customers.ListPagedAsync(
            predicate: c => customerIds.Contains(c.Id),
            orderBy: c => c.Name,
            pageNumber: 1,
            pageSize: customerIds.Count,
            cancellationToken: cancellationToken);

        return customers.Items.ToDictionary(c => c.Id, c => c.Name);
    }

    // -------------------------------------------------------------- validation

    private static List<string> ValidateShape(CreateQuotationRequest request)
    {
        var errors = new List<string>();

        if (request.CustomerId == Guid.Empty)
        {
            errors.Add("A customer must be selected.");
        }

        if (request.Lines.Count == 0)
        {
            errors.Add("A quotation must have at least one line.");
        }

        if (request.ValidUntil is not null && request.ValidUntil < request.QuotationDate)
        {
            errors.Add("Valid-until date cannot be earlier than the quotation date.");
        }

        for (var i = 0; i < request.Lines.Count; i++)
        {
            var line = request.Lines[i];
            var label = $"Line {i + 1}";

            if (line.ProductId == Guid.Empty)
            {
                errors.Add($"{label}: a product must be selected.");
            }

            if (line.Quantity <= 0)
            {
                errors.Add($"{label}: quantity must be greater than zero.");
            }

            if (line.UnitPrice < 0)
            {
                errors.Add($"{label}: unit price cannot be negative.");
            }

            if (line.DiscountPercent is < 0 or > 100)
            {
                errors.Add($"{label}: discount percent must be between 0 and 100.");
            }

            if (line.GstPercent < 0)
            {
                errors.Add($"{label}: GST percent cannot be negative.");
            }
        }

        return errors;
    }

    private static List<string> ValidatePaging(Guid companyId, int pageNumber, int pageSize)
    {
        var errors = new List<string>();

        if (companyId == Guid.Empty)
        {
            errors.Add("A company must be specified.");
        }

        if (pageNumber < 1)
        {
            errors.Add("Page number must be 1 or greater.");
        }

        if (pageSize < 1)
        {
            errors.Add("Page size must be 1 or greater.");
        }
        else if (pageSize > MaxPageSize)
        {
            errors.Add($"Page size must not exceed {MaxPageSize}.");
        }

        return errors;
    }

    // ------------------------------------------------------------- numbering

    /// <summary>
    /// Allocates the next number in the form <c>QT-{yyyy}-{0001}</c>, sequential per company
    /// per calendar year.
    /// </summary>
    /// <remarks>
    /// Derived from the highest existing number rather than a row count, because counting breaks
    /// as soon as anything is deleted. Read-then-write is not atomic, so two concurrent creates
    /// could compute the same number; the filtered unique index on
    /// <c>(CompanyId, QuotationNumber)</c> is the actual guarantee, and this method retries a few
    /// times before giving up. A database sequence would remove the race entirely and is the right
    /// fix if quotation creation ever becomes concurrent.
    /// </remarks>
    private async Task<string?> GenerateQuotationNumberAsync(
        Guid companyId,
        DateTime quotationDate,
        CancellationToken cancellationToken)
    {
        var year = quotationDate == default ? DateTime.UtcNow.Year : quotationDate.Year;
        var prefix = $"{NumberPrefix}-{year}-";

        var latest = await Quotations.ListPagedAsync(
            predicate: q => q.CompanyId == companyId && q.QuotationNumber.StartsWith(prefix),
            orderBy: q => q.QuotationNumber,
            pageNumber: 1,
            pageSize: 1,
            descending: true,
            cancellationToken);

        var next = 1;

        if (latest.Items.Count > 0)
        {
            var tail = latest.Items[0].QuotationNumber[prefix.Length..];

            if (int.TryParse(tail, out var parsed))
            {
                next = parsed + 1;
            }
            else
            {
                // An unparseable tail means someone introduced a different format. Fall back to
                // the total count rather than silently colliding on "0001".
                next = latest.TotalCount + 1;
            }
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var candidate = $"{prefix}{(next + attempt).ToString($"D{SequenceDigits}")}";

            var taken = await Quotations.AnyAsync(
                q => q.CompanyId == companyId && q.QuotationNumber == candidate,
                cancellationToken);

            if (!taken)
            {
                return candidate;
            }
        }

        return null;
    }

    // ------------------------------------------------------------------ mapping

    private static QuotationDto ToDto(
        Quotation q,
        string customerName,
        IReadOnlyList<QuotationLineDto> lines) => new()
    {
        Id = q.Id,
        CompanyId = q.CompanyId,
        QuotationNumber = q.QuotationNumber,
        CustomerId = q.CustomerId,
        CustomerName = customerName,
        QuotationDate = q.QuotationDate,
        ValidUntil = q.ValidUntil,
        SubTotal = q.SubTotal,
        DiscountAmount = q.DiscountAmount,
        TaxAmount = q.TaxAmount,
        TotalAmount = q.TotalAmount,
        Notes = q.Notes,
        Lines = lines,
    };

    private static QuotationLineDto ToLineDto(
        QuotationLine l,
        QuotationCalculator.LineAmounts amounts,
        string productName,
        string productSku) => new()
    {
        Id = l.Id,
        QuotationId = l.QuotationId,
        ProductId = l.ProductId,
        ProductName = productName,
        ProductSku = productSku,
        Quantity = l.Quantity,
        UnitPrice = l.UnitPrice,
        DiscountPercent = l.DiscountPercent,
        GstPercent = l.GstPercent,
        GrossAmount = amounts.Gross,
        DiscountAmount = amounts.Discount,
        TaxAmount = amounts.Tax,
        TotalAmount = amounts.Total,
    };
}
