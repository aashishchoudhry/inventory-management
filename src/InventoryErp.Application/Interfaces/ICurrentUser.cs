namespace InventoryErp.Application.Interfaces;

/// <summary>
/// Ambient identity of the caller, used for audit stamping. Implemented by the web layer
/// from the HTTP context; background jobs and tests supply their own.
/// </summary>
public interface ICurrentUser
{
    /// <summary>User name of the caller, or null when unauthenticated.</summary>
    string? UserName { get; }
}
