namespace InventoryErp.Shared.Constants;

/// <summary>
/// Identity role names. Used in <c>[Authorize(Roles = ...)]</c>, so these must stay
/// in sync with the roles seeded into the database.
/// </summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string StoreKeeper = "StoreKeeper";

    public static readonly IReadOnlyList<string> All = [Admin, Manager, StoreKeeper];
}
