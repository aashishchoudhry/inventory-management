using InventoryErp.Application.Services;

namespace InventoryErp.Application.Tests.Services;

/// <summary>
/// Pure arithmetic — no database. These pin down the order of operations and the rounding rule.
/// </summary>
public class QuotationCalculatorTests
{
    [Fact]
    public void No_discount_no_tax_is_just_quantity_times_price()
    {
        var r = QuotationCalculator.CalculateLine(3, 100m, discountPercent: 0m, gstPercent: 0m);

        Assert.Equal(300m, r.Gross);
        Assert.Equal(0m, r.Discount);
        Assert.Equal(300m, r.Taxable);
        Assert.Equal(0m, r.Tax);
        Assert.Equal(300m, r.Total);
    }

    [Fact]
    public void Tax_only_is_charged_on_the_gross()
    {
        var r = QuotationCalculator.CalculateLine(2, 500m, discountPercent: 0m, gstPercent: 18m);

        Assert.Equal(1000m, r.Gross);
        Assert.Equal(180m, r.Tax);
        Assert.Equal(1180m, r.Total);
    }

    [Fact]
    public void Gst_is_charged_after_discount_not_before()
    {
        // 10 × 100 = 1000 gross; 10% discount = 100; taxable 900; GST 18% of 900 = 162.
        // Taxing the gross instead would give 180 — an 18.00 overstatement on this line alone.
        var r = QuotationCalculator.CalculateLine(10, 100m, discountPercent: 10m, gstPercent: 18m);

        Assert.Equal(1000m, r.Gross);
        Assert.Equal(100m, r.Discount);
        Assert.Equal(900m, r.Taxable);
        Assert.Equal(162m, r.Tax);
        Assert.Equal(1062m, r.Total);
    }

    [Fact]
    public void A_hundred_percent_discount_leaves_nothing_to_tax()
    {
        var r = QuotationCalculator.CalculateLine(4, 250m, discountPercent: 100m, gstPercent: 18m);

        Assert.Equal(1000m, r.Gross);
        Assert.Equal(1000m, r.Discount);
        Assert.Equal(0m, r.Taxable);
        Assert.Equal(0m, r.Tax);
        Assert.Equal(0m, r.Total);
    }

    [Theory]
    // Indian GST slabs, on a price that does not divide cleanly.
    [InlineData(5, 349.00, 0, 5, 1745.00, 0, 87.25, 1832.25)]
    [InlineData(1, 8499.00, 0, 18, 8499.00, 0, 1529.82, 10028.82)]
    [InlineData(3, 1890.00, 0, 28, 5670.00, 0, 1587.60, 7257.60)]
    [InlineData(7, 425.00, 12.5, 12, 2975.00, 371.88, 312.37, 2915.49)]
    public void Slab_and_discount_combinations_round_to_two_places(
        int qty, decimal price, decimal discountPct, decimal gstPct,
        decimal expectedGross, decimal expectedDiscount, decimal expectedTax, decimal expectedTotal)
    {
        var r = QuotationCalculator.CalculateLine(qty, price, discountPct, gstPct);

        Assert.Equal(expectedGross, r.Gross);
        Assert.Equal(expectedDiscount, r.Discount);
        Assert.Equal(expectedTax, r.Tax);
        Assert.Equal(expectedTotal, r.Total);
    }

    [Fact]
    public void Rounds_half_away_from_zero_not_to_even()
    {
        // 0.125 → 0.13 under commercial rounding. Banker's rounding would give 0.12,
        // which is the .NET default and is not what invoices use.
        var r = QuotationCalculator.CalculateLine(1, 2.50m, discountPercent: 0m, gstPercent: 5m);

        Assert.Equal(0.13m, r.Tax);
        Assert.Equal(2.63m, r.Total);
    }

    [Fact]
    public void Every_amount_is_at_most_two_decimal_places()
    {
        var r = QuotationCalculator.CalculateLine(3, 33.333m, discountPercent: 7.77m, gstPercent: 18m);

        Assert.Equal(r.Gross, decimal.Round(r.Gross, 2));
        Assert.Equal(r.Discount, decimal.Round(r.Discount, 2));
        Assert.Equal(r.Tax, decimal.Round(r.Tax, 2));
        Assert.Equal(r.Total, decimal.Round(r.Total, 2));
    }

    [Fact]
    public void Total_always_reconciles_with_its_parts()
    {
        var r = QuotationCalculator.CalculateLine(13, 77.77m, discountPercent: 3.5m, gstPercent: 12m);

        Assert.Equal(r.Gross - r.Discount, r.Taxable);
        Assert.Equal(r.Taxable + r.Tax, r.Total);
    }
}
