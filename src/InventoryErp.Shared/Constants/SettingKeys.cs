namespace InventoryErp.Shared.Constants;

/// <summary>
/// Keys for values stored in the runtime settings store (database-backed), as opposed to
/// deployment configuration in appsettings.json.
/// </summary>
public static class SettingKeys
{
    public static class Company
    {
        public const string Name = "Company.Name";
        public const string AddressLine = "Company.AddressLine";
        public const string TaxId = "Company.TaxId";
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
        public const string PdfFooterText = "Documents.PdfFooterText";
    }
}
