namespace InventoryErp.Application.Services;

/// <summary>
/// The money maths for a quotation, kept separate from persistence so it can be reasoned about
/// and tested on its own.
/// </summary>
/// <remarks>
/// <para><b>Order of operations.</b> Discount is applied first, then GST is charged on the
/// discounted amount — not on the gross. This matches Indian GST practice, where tax is levied on
/// the transaction value after any discount shown on the invoice. Taxing the gross would overstate
/// the tax due on every discounted line.</para>
/// <para><b>Rounding.</b> Every monetary figure is rounded to 2 decimal places at the point it is
/// produced, using away-from-zero (commercial) rounding rather than .NET's default banker's
/// rounding. Header totals are then sums of already-rounded line figures, so the stored header
/// always equals the sum of the stored lines — no cent-level drift between the two.</para>
/// </remarks>
internal static class QuotationCalculator
{
    private const int MoneyScale = 2;

    internal readonly record struct LineAmounts(
        decimal Gross,
        decimal Discount,
        decimal Taxable,
        decimal Tax,
        decimal Total);

    internal static LineAmounts CalculateLine(
        int quantity,
        decimal unitPrice,
        decimal discountPercent,
        decimal gstPercent)
    {
        var gross = Round(quantity * unitPrice);
        var discount = Round(gross * discountPercent / 100m);
        var taxable = gross - discount;
        var tax = Round(taxable * gstPercent / 100m);

        // Already at 2dp: taxable and tax are both rounded, so their sum needs no further rounding.
        var total = taxable + tax;

        return new LineAmounts(gross, discount, taxable, tax, total);
    }

    internal static decimal Round(decimal value)
        => Math.Round(value, MoneyScale, MidpointRounding.AwayFromZero);
}
