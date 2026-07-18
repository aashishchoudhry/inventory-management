using InventoryErp.Application.Common;

namespace InventoryErp.Application.Interfaces;

/// <summary>
/// Stores company logo images and returns the web path to reference them by.
/// </summary>
/// <remarks>
/// An abstraction rather than direct file access so the Application layer stays free of
/// <c>IWebHostEnvironment</c> and <c>System.IO</c> paths, and so a future move to blob storage
/// changes only the implementation.
/// </remarks>
public interface ILogoStorage
{
    /// <summary>
    /// Validates and saves an image, returning its root-relative web path
    /// (e.g. <c>/uploads/logos/{guid}.png</c>).
    /// </summary>
    /// <returns><c>ValidationFailed</c> for an unsupported type or oversized file.</returns>
    Task<ServiceResult<string>> SaveAsync(
        LogoUpload upload,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a previously stored logo. Missing files are ignored.</summary>
    Task DeleteAsync(string webPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a stored logo's bytes, or returns <c>null</c> when the path is blank, outside the
    /// logo directory, or the file is no longer on disk.
    /// </summary>
    /// <remarks>
    /// Returns null rather than throwing because the database and the filesystem can disagree —
    /// a restored backup without <c>wwwroot/uploads</c> leaves every <c>LogoPath</c> dangling.
    /// Callers rendering documents must degrade to "no logo", never fail the document.
    /// </remarks>
    Task<byte[]?> TryReadAsync(string? webPath, CancellationToken cancellationToken = default);
}

/// <summary>
/// An uploaded image, decoupled from ASP.NET's <c>IFormFile</c> so Application does not
/// reference the web framework.
/// </summary>
public sealed record LogoUpload(string FileName, string ContentType, long Length, Stream Content);
