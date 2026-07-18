namespace InventoryErp.Web.Models;

/// <summary>Model for the shared <c>_EmptyState</c> partial.</summary>
public sealed class EmptyStateViewModel
{
    /// <summary>Icon symbol id from the sprite, without the leading '#'.</summary>
    public string Icon { get; init; } = "i-inbox";

    public string Title { get; init; } = "Nothing here yet";

    public string Message { get; init; } = string.Empty;

    public string? ActionText { get; init; }

    public string? ActionController { get; init; }

    public string? ActionAction { get; init; }
}
