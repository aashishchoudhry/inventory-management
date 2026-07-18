using InventoryErp.Domain.Entities;
using InventoryErp.Domain.Enums;

namespace InventoryErp.Infrastructure.Persistence.Seeding;

/// <summary>
/// The sample data written on first run. Kept separate from <see cref="DatabaseSeeder"/> so the
/// values are easy to review and change without touching the seeding logic.
/// </summary>
internal static class SeedData
{
    /// <summary>
    /// Credentials for the seeded administrator. Development convenience only — this account
    /// must not exist in any deployed environment.
    /// </summary>
    public const string AdminEmail = "admin@inventoryerp.local";

    public const string AdminPassword = "Admin@123456";

    public const string AdminFullName = "System Administrator";

    public static Company CreateCompany() => new()
    {
        Name = "Sharma Industrial Supplies Pvt Ltd",
        Tagline = "Fasteners, tools and workshop consumables since 1998",
        Address = "Plot 47, MIDC Industrial Estate, Andheri East",
        City = "Mumbai",
        State = "Maharashtra",
        Country = "India",
        PinCode = "400093",
        GstNumber = "27AABCU9603R1ZM",
        PanNumber = "AABCU9603R",
        Mobile = "+91 98200 45671",
        Email = "accounts@sharmaindustrial.in",
        Website = "https://www.sharmaindustrial.in",
        LogoPath = "/images/company-logo.png",
    };

    /// <summary>GST rates follow the Indian slabs: 5, 12, 18 and 28 percent.</summary>
    public static IReadOnlyList<Product> CreateProducts(Guid companyId) =>
    [
        new()
        {
            CompanyId = companyId,
            Name = "Hex Bolt M10 x 50mm (Stainless Steel)",
            Sku = "FST-HB-M10-50",
            Barcode = "8901234500017",
            Description = "Grade 304 stainless steel hex bolt, DIN 933.",
            SellingPrice = 24.50m,
            GstPercent = 18m,
            CurrentStock = 1450,
            ReorderLevel = 300,
            Status = ProductStatus.Active,
        },
        new()
        {
            CompanyId = companyId,
            Name = "Hex Nut M10 (Zinc Plated)",
            Sku = "FST-HN-M10",
            Barcode = "8901234500024",
            Description = "Zinc plated mild steel hex nut, DIN 934.",
            SellingPrice = 6.75m,
            GstPercent = 18m,
            CurrentStock = 120,
            ReorderLevel = 500,
            Status = ProductStatus.Active,
        },
        new()
        {
            CompanyId = companyId,
            Name = "Cordless Impact Drill 18V",
            Sku = "TOOL-CID-18V",
            Barcode = "8901234500031",
            Description = "Brushless 18V impact drill with two 4.0Ah batteries and charger.",
            SellingPrice = 8_499.00m,
            GstPercent = 18m,
            CurrentStock = 23,
            ReorderLevel = 10,
            Status = ProductStatus.Active,
        },
        new()
        {
            CompanyId = companyId,
            Name = "Safety Helmet (Yellow, ISI Marked)",
            Sku = "PPE-HLM-YEL",
            Barcode = "8901234500048",
            Description = "HDPE industrial safety helmet with ratchet suspension. IS 2925.",
            SellingPrice = 349.00m,
            GstPercent = 5m,
            CurrentStock = 87,
            ReorderLevel = 40,
            Status = ProductStatus.Active,
        },
        new()
        {
            CompanyId = companyId,
            Name = "Cut-Off Wheel 4 inch (Pack of 50)",
            Sku = "ABR-COW-4-50",
            Barcode = "8901234500055",
            Description = "Reinforced abrasive cut-off wheel for angle grinders, 4in x 1.2mm.",
            SellingPrice = 1_275.00m,
            GstPercent = 18m,
            CurrentStock = 8,
            ReorderLevel = 15,
            Status = ProductStatus.Active,
        },
        new()
        {
            CompanyId = companyId,
            Name = "Industrial Degreaser 5L",
            Sku = "CHM-DEG-5L",
            Barcode = "8901234500062",
            Description = "Water-based heavy duty degreaser concentrate.",
            SellingPrice = 1_890.00m,
            GstPercent = 28m,
            CurrentStock = 34,
            ReorderLevel = 12,
            Status = ProductStatus.Active,
        },
        new()
        {
            CompanyId = companyId,
            Name = "Measuring Tape 8m Steel",
            Sku = "MSR-TAP-8M",
            Barcode = "8901234500079",
            Description = "Steel measuring tape with nylon coating and belt clip.",
            SellingPrice = 425.00m,
            GstPercent = 12m,
            CurrentStock = 156,
            ReorderLevel = 50,
            Status = ProductStatus.Active,
        },
        new()
        {
            CompanyId = companyId,
            Name = "Ball Bearing 6204ZZ (Legacy)",
            Sku = "BRG-6204ZZ",
            Barcode = null,
            Description = "Discontinued line, retained for historical quotations.",
            SellingPrice = 142.00m,
            GstPercent = 18m,
            CurrentStock = 0,
            ReorderLevel = 0,
            Status = ProductStatus.Discontinued,
        },
    ];

    public static IReadOnlyList<Customer> CreateCustomers(Guid companyId) =>
    [
        new()
        {
            CompanyId = companyId,
            Name = "Patel Engineering Works",
            Code = "CUST-1001",
            Mobile = "+91 98795 22310",
            City = "Ahmedabad",
            State = "Gujarat",
            Address = "12 Phase II, GIDC Vatva",
        },
        new()
        {
            CompanyId = companyId,
            Name = "Deccan Fabricators",
            Code = "CUST-1002",
            Mobile = "+91 90080 71422",
            City = "Hyderabad",
            State = "Telangana",
            Address = "Survey 88, Balanagar Industrial Area",
        },
        new()
        {
            CompanyId = companyId,
            Name = "Coastal Marine Services",
            Code = "CUST-1003",
            Mobile = "+91 94470 65189",
            City = "Kochi",
            State = "Kerala",
            Address = "Warehouse 3, Willingdon Island",
        },
        new()
        {
            CompanyId = companyId,
            Name = "Northern Auto Components",
            Code = "CUST-1004",
            Mobile = "+91 98140 33756",
            City = "Ludhiana",
            State = "Punjab",
            Address = "Focal Point Phase VII",
        },
        new()
        {
            CompanyId = companyId,
            Name = "Sunrise Construction Co.",
            Code = "CUST-1005",
            Mobile = "+91 96500 18294",
            City = "Jaipur",
            State = "Rajasthan",
            Address = "B-22 Malviya Industrial Area",
        },
        new()
        {
            CompanyId = companyId,
            Name = "Bengal Textile Mills",
            Code = "CUST-1006",
            Mobile = "+91 98300 47612",
            City = "Kolkata",
            State = "West Bengal",
            Address = "7 Canal South Road, Beliaghata",
        },
        new()
        {
            // Deliberately has no Code, exercising the "optional but unique" index filter.
            CompanyId = companyId,
            Name = "Walk-in / Counter Sales",
            Code = null,
            Mobile = null,
            City = "Mumbai",
            State = "Maharashtra",
            Address = null,
        },
    ];
}
