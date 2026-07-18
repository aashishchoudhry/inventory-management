namespace InventoryErp.Application.DTOs.Quotations;

public sealed record QuotationDto
{
    public Guid Id { get; init; }
    public Guid CompanyId { get; init; }
    public string QuotationNumber { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }

    /// <summary>Resolved for display. The entity has no navigation property.</summary>
    public string CustomerName { get; init; } = string.Empty;

    public DateTime QuotationDate { get; init; }
    public DateTime? ValidUntil { get; init; }

    /// <summary>Sum of line gross amounts, before discount and tax.</summary>
    public decimal SubTotal { get; init; }

    /// <summary>Sum of line discounts.</summary>
    public decimal DiscountAmount { get; init; }

    /// <summary>Sum of line taxes, each computed on the post-discount amount.</summary>
    public decimal TaxAmount { get; init; }

    /// <summary><c>SubTotal - DiscountAmount + TaxAmount</c>.</summary>
    public decimal TotalAmount { get; init; }

    public string? Notes { get; init; }

    public IReadOnlyList<QuotationLineDto> Lines { get; init; } = [];
}

public sealed record QuotationLineDto
{
    public Guid Id { get; init; }
    public Guid QuotationId { get; init; }
    public Guid ProductId { get; init; }

    /// <summary>Resolved for display. The entity has no navigation property.</summary>
    public string ProductName { get; init; } = string.Empty;

    public string ProductSku { get; init; } = string.Empty;

    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal DiscountPercent { get; init; }
    public decimal GstPercent { get; init; }

    /// <summary><c>Quantity × UnitPrice</c>, before discount and tax. Computed, not persisted.</summary>
    public decimal GrossAmount { get; init; }

    /// <summary>Computed from <see cref="DiscountPercent"/>. Not persisted on the line.</summary>
    public decimal DiscountAmount { get; init; }

    public decimal TaxAmount { get; init; }

    /// <summary><c>GrossAmount - DiscountAmount + TaxAmount</c>.</summary>
    public decimal TotalAmount { get; init; }
}

/// <summary>Row shape for the quotation list — header fields only, no lines.</summary>
public sealed record QuotationListItemDto
{
    public Guid Id { get; init; }
    public string QuotationNumber { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public DateTime QuotationDate { get; init; }
    public DateTime? ValidUntil { get; init; }
    public decimal TotalAmount { get; init; }
    public int LineCount { get; init; }
}
