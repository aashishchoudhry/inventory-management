using InventoryErp.Application.Common;
using InventoryErp.Application.Interfaces;

namespace InventoryErp.Web.Services;

/// <summary>
/// Saves logos under <c>wwwroot/uploads/logos</c>. Lives in the web project because that is
/// where <c>wwwroot</c> is; the Application layer sees only <see cref="ILogoStorage"/>.
/// </summary>
public sealed class LogoStorage : ILogoStorage
{
    public const long MaxBytes = 2 * 1024 * 1024;

    private const string RelativeDirectory = "uploads/logos";

    /// <summary>
    /// Allowed extensions mapped to the magic bytes that must start the file. The extension and
    /// content type are both caller-supplied and trivially forged, so the signature is what is
    /// actually trusted.
    /// </summary>
    private static readonly Dictionary<string, byte[][]> Signatures = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = [[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]],
        [".jpg"] = [[0xFF, 0xD8, 0xFF]],
        [".jpeg"] = [[0xFF, 0xD8, 0xFF]],
    };

    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<LogoStorage> _logger;

    public LogoStorage(IWebHostEnvironment environment, ILogger<LogoStorage> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public async Task<ServiceResult<string>> SaveAsync(
        LogoUpload upload,
        CancellationToken cancellationToken = default)
    {
        if (upload.Length <= 0)
        {
            return ServiceResult<string>.Invalid("The selected file is empty.");
        }

        if (upload.Length > MaxBytes)
        {
            return ServiceResult<string>.Invalid(
                $"The logo must be {MaxBytes / 1024 / 1024} MB or smaller.");
        }

        var extension = Path.GetExtension(upload.FileName);

        if (string.IsNullOrEmpty(extension) || !Signatures.TryGetValue(extension, out var expected))
        {
            return ServiceResult<string>.Invalid("The logo must be a PNG or JPG image.");
        }

        if (!await HasMatchingSignatureAsync(upload.Content, expected, cancellationToken))
        {
            return ServiceResult<string>.Invalid(
                "That file is not a valid PNG or JPG image.");
        }

        var directory = Path.Combine(_environment.WebRootPath, RelativeDirectory);
        Directory.CreateDirectory(directory);

        // Generated name: never trust the client's filename, which could contain path traversal
        // segments or overwrite an existing file.
        var storedName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var fullPath = Path.Combine(directory, storedName);

        upload.Content.Position = 0;

        await using (var destination = File.Create(fullPath))
        {
            await upload.Content.CopyToAsync(destination, cancellationToken);
        }

        var webPath = $"/{RelativeDirectory}/{storedName}";
        _logger.LogInformation("Stored company logo at {WebPath}.", webPath);

        return ServiceResult<string>.Success(webPath);
    }

    public Task DeleteAsync(string webPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(webPath))
        {
            return Task.CompletedTask;
        }

        // Only ever delete inside the logos directory, whatever the stored value claims.
        var name = Path.GetFileName(webPath);
        var expectedPrefix = $"/{RelativeDirectory}/";

        if (!webPath.StartsWith(expectedPrefix, StringComparison.Ordinal) || string.IsNullOrEmpty(name))
        {
            return Task.CompletedTask;
        }

        var fullPath = Path.Combine(_environment.WebRootPath, RelativeDirectory, name);

        try
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch (IOException ex)
        {
            // A stale file is not worth failing the user's save for.
            _logger.LogWarning(ex, "Could not delete old logo {WebPath}.", webPath);
        }

        return Task.CompletedTask;
    }

    public async Task<byte[]?> TryReadAsync(string? webPath, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveStoredPath(webPath);

        if (fullPath is null || !File.Exists(fullPath))
        {
            // Not an error: the row can outlive the file (restored backup, manual deletion).
            _logger.LogWarning(
                "Logo path {WebPath} is set but the file is missing; rendering without a logo.",
                webPath);
            return null;
        }

        try
        {
            return await File.ReadAllBytesAsync(fullPath, cancellationToken);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Could not read logo {WebPath}; rendering without a logo.", webPath);
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "No access to logo {WebPath}; rendering without a logo.", webPath);
            return null;
        }
    }

    /// <summary>
    /// Maps a stored web path to a full path, or null if it does not name a file inside the
    /// logos directory. Guards against a tampered value escaping the upload folder.
    /// </summary>
    private string? ResolveStoredPath(string? webPath)
    {
        if (string.IsNullOrWhiteSpace(webPath))
        {
            return null;
        }

        var expectedPrefix = $"/{RelativeDirectory}/";

        if (!webPath.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var name = Path.GetFileName(webPath);

        return string.IsNullOrEmpty(name)
            ? null
            : Path.Combine(_environment.WebRootPath, RelativeDirectory, name);
    }

    private static async Task<bool> HasMatchingSignatureAsync(
        Stream content,
        byte[][] expected,
        CancellationToken cancellationToken)
    {
        var longest = expected.Max(s => s.Length);
        var buffer = new byte[longest];

        content.Position = 0;
        var read = await content.ReadAtLeastAsync(buffer, longest, throwOnEndOfStream: false, cancellationToken);
        content.Position = 0;

        return read >= longest
            && expected.Any(signature => buffer.Take(signature.Length).SequenceEqual(signature));
    }
}
