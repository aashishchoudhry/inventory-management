namespace InventoryErp.Shared.Constants;

/// <summary>
/// Keys for values stored in the runtime settings store (<c>CompanySetting</c> rows), as opposed
/// to deployment configuration in appsettings.json.
/// </summary>
/// <remarks>
/// Several of these mirror typed columns on the <c>Company</c> entity (address, GSTIN, PAN).
/// Where both exist, the <b>setting wins and the column is the fallback</b> — see
/// <c>SettingsService</c>. Keys are string literals in the database, so changing one orphans
/// existing rows; add a new key rather than renaming.
/// </remarks>
public static class SettingKeys
{
    public static class Company
    {
        public const string Name = "Company.Name";
        public const string Tagline = "Company.Tagline";
        public const string Mobile = "Company.Mobile";
        public const string Email = "Company.Email";
        public const string Website = "Company.Website";
        public const string AddressLine = "Company.AddressLine";
        public const string City = "Company.City";
        public const string State = "Company.State";
        public const string Country = "Company.Country";
        public const string PinCode = "Company.PinCode";
        public const string Gstin = "Company.Gstin";
        public const string Pan = "Company.Pan";
        public const string LogoPath = "Company.LogoPath";
    }

    public static class Inventory
    {
        public const string LowStockThreshold = "Inventory.LowStockThreshold";
        public const string AllowNegativeStock = "Inventory.AllowNegativeStock";
        public const string SkuPrefix = "Inventory.SkuPrefix";
    }

    public static class Documents
    {
        public const string InvoiceNumberPrefix = "Documents.InvoiceNumberPrefix";
        public const string InvoiceTerms = "Documents.InvoiceTerms";
        public const string InvoiceFooter = "Documents.InvoiceFooter";
    }

    public static class Branding
    {
        public const string PrimaryAccentColor = "Branding.PrimaryAccentColor";
    }
}
