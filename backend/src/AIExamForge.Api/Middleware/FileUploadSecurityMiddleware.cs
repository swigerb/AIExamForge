using System.Security.Cryptography;

namespace AIExamForge.Api.Middleware;

/// <summary>
/// T164: File upload security per FR-001e, NFR-008.
/// Validates file size, content type, extension, and provides malware scan hook.
/// </summary>
public sealed class FileUploadSecurityMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<FileUploadSecurityMiddleware> _logger;
    private readonly FileUploadSecurityOptions _options;

    // Magic bytes for allowed file types
    private static readonly Dictionary<string, byte[][]> AllowedFileMagicBytes = new()
    {
        // PDF: %PDF
        [".pdf"] = [
            [0x25, 0x50, 0x44, 0x46]
        ],
        // DOCX: PK (ZIP archive)
        [".docx"] = [
            [0x50, 0x4B, 0x03, 0x04],
            [0x50, 0x4B, 0x05, 0x06],
            [0x50, 0x4B, 0x07, 0x08]
        ],
        // JSON (text files - check for valid JSON structure)
        [".json"] = [],
        // TXT (text files)
        [".txt"] = []
    };

    public FileUploadSecurityMiddleware(
        RequestDelegate next,
        ILogger<FileUploadSecurityMiddleware> logger,
        FileUploadSecurityOptions? options = null)
    {
        _next = next;
        _logger = logger;
        _options = options ?? new FileUploadSecurityOptions();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only process file uploads (multipart/form-data)
        if (!IsFileUpload(context.Request))
        {
            await _next(context);
            return;
        }

        // Check total content length
        if (context.Request.ContentLength.HasValue &&
            context.Request.ContentLength.Value > _options.MaxTotalUploadSize)
        {
            _logger.LogWarning(
                "Upload too large: {Size} bytes exceeds max {MaxSize} bytes",
                context.Request.ContentLength.Value, _options.MaxTotalUploadSize);

            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.11",
                title = "Payload Too Large",
                status = 413,
                detail = $"Total upload size exceeds {_options.MaxTotalUploadSize / (1024 * 1024)}MB limit."
            });
            return;
        }

        await _next(context);
    }

    private static bool IsFileUpload(HttpRequest request)
    {
        return request.ContentType?.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase) == true;
    }
}

/// <summary>
/// Options for file upload security.
/// </summary>
public sealed class FileUploadSecurityOptions
{
    /// <summary>
    /// Maximum total upload size in bytes. Default: 25MB per FR-001e.
    /// </summary>
    public long MaxTotalUploadSize { get; set; } = 25 * 1024 * 1024;

    /// <summary>
    /// Maximum individual file size in bytes. Default: 10MB.
    /// </summary>
    public long MaxFileSize { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Allowed file extensions. Default: .pdf, .docx, .txt
    /// </summary>
    public HashSet<string> AllowedExtensions { get; set; } = [".pdf", ".docx", ".txt"];

    /// <summary>
    /// Allowed MIME types.
    /// </summary>
    public HashSet<string> AllowedMimeTypes { get; set; } =
    [
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "text/plain"
    ];

    /// <summary>
    /// Enable content type validation (magic bytes check).
    /// </summary>
    public bool ValidateContentType { get; set; } = true;

    /// <summary>
    /// Hook for malware scanning. Returns true if file is clean.
    /// </summary>
    public Func<Stream, string, CancellationToken, Task<bool>>? MalwareScanHook { get; set; }
}

/// <summary>
/// File upload validation service used by endpoints.
/// Per FR-001e: Size check, content-type validation, extension check.
/// Per NFR-008: Security scanning.
/// </summary>
public interface IFileUploadValidator
{
    /// <summary>
    /// Validates an uploaded file.
    /// </summary>
    Task<FileValidationResult> ValidateAsync(
        IFormFile file,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of file validation.
/// </summary>
public sealed record FileValidationResult
{
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }
    public string? SanitizedFileName { get; init; }
    public string? DetectedContentType { get; init; }

    public static FileValidationResult Valid(string sanitizedFileName, string contentType) =>
        new() { IsValid = true, SanitizedFileName = sanitizedFileName, DetectedContentType = contentType };

    public static FileValidationResult Invalid(string error) =>
        new() { IsValid = false, ErrorMessage = error };
}

/// <summary>
/// Implementation of file upload validation.
/// </summary>
public sealed class FileUploadValidator : IFileUploadValidator
{
    private readonly FileUploadSecurityOptions _options;
    private readonly ILogger<FileUploadValidator> _logger;

    // Magic bytes for file type detection
    private static readonly Dictionary<string, byte[][]> MagicBytes = new()
    {
        [".pdf"] = [[0x25, 0x50, 0x44, 0x46]], // %PDF
        [".docx"] = [[0x50, 0x4B, 0x03, 0x04]], // PK (ZIP)
    };

    public FileUploadValidator(
        FileUploadSecurityOptions options,
        ILogger<FileUploadValidator> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<FileValidationResult> ValidateAsync(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        // Validate file size
        if (file.Length == 0)
        {
            return FileValidationResult.Invalid("File is empty.");
        }

        if (file.Length > _options.MaxFileSize)
        {
            _logger.LogWarning(
                "File {FileName} too large: {Size} bytes exceeds {MaxSize} bytes",
                file.FileName, file.Length, _options.MaxFileSize);
            return FileValidationResult.Invalid(
                $"File size exceeds the {_options.MaxFileSize / (1024 * 1024)}MB limit.");
        }

        // Validate and sanitize filename
        var sanitizedFileName = SanitizeFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(sanitizedFileName))
        {
            return FileValidationResult.Invalid("Invalid file name.");
        }

        // Validate extension
        var extension = Path.GetExtension(sanitizedFileName).ToLowerInvariant();
        if (!_options.AllowedExtensions.Contains(extension))
        {
            _logger.LogWarning(
                "File {FileName} has disallowed extension: {Extension}",
                file.FileName, extension);
            return FileValidationResult.Invalid(
                $"File type '{extension}' is not allowed. Allowed types: {string.Join(", ", _options.AllowedExtensions)}");
        }

        // Validate MIME type
        var mimeType = file.ContentType?.ToLowerInvariant() ?? "";
        if (!string.IsNullOrEmpty(mimeType) && !_options.AllowedMimeTypes.Contains(mimeType))
        {
            _logger.LogWarning(
                "File {FileName} has disallowed MIME type: {MimeType}",
                file.FileName, mimeType);
            return FileValidationResult.Invalid(
                $"Content type '{mimeType}' is not allowed.");
        }

        // Validate magic bytes if enabled
        if (_options.ValidateContentType && MagicBytes.ContainsKey(extension))
        {
            using var stream = file.OpenReadStream();
            var isValidMagic = await ValidateMagicBytesAsync(stream, extension, cancellationToken);
            if (!isValidMagic)
            {
                _logger.LogWarning(
                    "File {FileName} content does not match extension {Extension}",
                    file.FileName, extension);
                return FileValidationResult.Invalid(
                    "File content does not match the declared file type.");
            }
        }

        // Check for password-protected PDF
        if (extension == ".pdf")
        {
            using var stream = file.OpenReadStream();
            if (await IsPdfEncryptedAsync(stream, cancellationToken))
            {
                return FileValidationResult.Invalid(
                    "Password-protected PDFs are not supported in v1. Please remove the password or provide a plaintext/Word document.");
            }
        }

        // Run malware scan if hook is configured
        if (_options.MalwareScanHook != null)
        {
            using var stream = file.OpenReadStream();
            var isClean = await _options.MalwareScanHook(stream, sanitizedFileName, cancellationToken);
            if (!isClean)
            {
                _logger.LogWarning("File {FileName} failed malware scan", file.FileName);
                return FileValidationResult.Invalid("File failed security scan.");
            }
        }

        return FileValidationResult.Valid(sanitizedFileName, mimeType);
    }

    private static string SanitizeFileName(string fileName)
    {
        // Remove path components
        fileName = Path.GetFileName(fileName);

        // Remove invalid characters
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("", fileName.Where(c => !invalidChars.Contains(c)));

        // Limit length
        if (sanitized.Length > 255)
        {
            var ext = Path.GetExtension(sanitized);
            var name = Path.GetFileNameWithoutExtension(sanitized);
            sanitized = name[..(255 - ext.Length)] + ext;
        }

        return sanitized;
    }

    private static async Task<bool> ValidateMagicBytesAsync(
        Stream stream,
        string extension,
        CancellationToken cancellationToken)
    {
        if (!MagicBytes.TryGetValue(extension, out var expectedMagicArrays))
        {
            return true; // No magic bytes defined for this type
        }

        var maxMagicLength = expectedMagicArrays.Max(m => m.Length);
        var buffer = new byte[maxMagicLength];
        var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, maxMagicLength), cancellationToken);

        // Reset stream position
        stream.Position = 0;

        foreach (var expectedMagic in expectedMagicArrays)
        {
            if (bytesRead >= expectedMagic.Length &&
                buffer.AsSpan(0, expectedMagic.Length).SequenceEqual(expectedMagic))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<bool> IsPdfEncryptedAsync(Stream stream, CancellationToken cancellationToken)
    {
        // Read first 4KB to check for encryption dictionary
        var buffer = new byte[4096];
        var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
        stream.Position = 0;

        var content = System.Text.Encoding.ASCII.GetString(buffer, 0, bytesRead);

        // Check for encryption markers in PDF
        return content.Contains("/Encrypt", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Extension methods for file upload security.
/// </summary>
public static class FileUploadSecurityExtensions
{
    public static IServiceCollection AddFileUploadSecurity(
        this IServiceCollection services,
        Action<FileUploadSecurityOptions>? configure = null)
    {
        var options = new FileUploadSecurityOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddScoped<IFileUploadValidator, FileUploadValidator>();

        return services;
    }

    public static IApplicationBuilder UseFileUploadSecurity(
        this IApplicationBuilder app,
        FileUploadSecurityOptions? options = null)
    {
        return app.UseMiddleware<FileUploadSecurityMiddleware>(options ?? new FileUploadSecurityOptions());
    }
}
