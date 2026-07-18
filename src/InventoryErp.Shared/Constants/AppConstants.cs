namespace InventoryErp.Shared.Constants;

/// <summary>
/// Application-wide literals that are not user-configurable.
/// </summary>
public static class AppConstants
{
    public const string ApplicationName = "InventoryErp";

    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 200;

    public const string DateFormat = "yyyy-MM-dd";
    public const string DateTimeFormat = "yyyy-MM-dd HH:mm";

    /// <summary>Culture used for money formatting until per-tenant settings exist.</summary>
    public const string DefaultCulture = "en-IN";
}
