namespace InventoryErp.Application.Interfaces;

/// <summary>
/// Renders a document model to PDF bytes. The concrete implementation lives in
/// Infrastructure; no library has been chosen yet, so there is no implementation
/// registered and injecting this will fail until one is added.
/// </summary>
public interface IPdfGenerator
{
    Task<byte[]> RenderAsync(string templateName, object model, CancellationToken cancellationToken = default);
}
