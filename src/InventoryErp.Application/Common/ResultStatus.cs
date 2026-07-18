namespace InventoryErp.Application.Common;

/// <summary>
/// Outcome classification for <see cref="ServiceResult"/>. The web layer maps these onto
/// HTTP responses / view behaviour, which is why failure kinds are distinguished here
/// rather than by exception type.
/// </summary>
public enum ResultStatus
{
    Success = 0,
    NotFound = 1,
    ValidationFailed = 2,
    Conflict = 3,
    Unauthorized = 4,
    Error = 5,
}
