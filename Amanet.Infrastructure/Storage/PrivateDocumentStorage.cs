using System.Security.Cryptography;
using Amanet.Application.Exceptions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PdfSharp.Pdf.Content;
using PdfSharp.Pdf.IO;

namespace Amanet.Infrastructure.Storage;

public sealed class PrivateDocumentStorage
{
    public const long MaximumBytes = 20 * 1024 * 1024;
    public const long MaximumSignedBytes = 25 * 1024 * 1024;
    private readonly string root;
    private readonly ILogger<PrivateDocumentStorage> logger;

    public PrivateDocumentStorage(IWebHostEnvironment environment, IConfiguration configuration,
        ILogger<PrivateDocumentStorage> logger)
    {
        this.logger = logger;
        root = Path.GetFullPath(configuration["Documents:StoragePath"] ?? "private-uploads", environment.ContentRootPath);
        var webRoot = Path.GetFullPath(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (root.Equals(webRoot, comparison) || root.StartsWith(Path.TrimEndingDirectorySeparator(webRoot) + Path.DirectorySeparatorChar, comparison))
            throw new InvalidOperationException("Document storage must be outside the web root.");

        Directory.CreateDirectory(Path.Combine(root, ".staging"));
    }

    internal Task<StoredPdf> SaveAsync(Stream content, CancellationToken cancellationToken)
    {
        return WriteAsync(content, MaximumBytes, true, cancellationToken);
    }

    internal Task<StoredPdf> StageGeneratedAsync(Stream content, CancellationToken cancellationToken)
    {
        return WriteAsync(content, MaximumSignedBytes, false, cancellationToken);
    }

    private async Task<StoredPdf> WriteAsync(Stream content, long maximumBytes, bool publish, CancellationToken cancellationToken)
    {
        var key = Guid.NewGuid().ToString("N") + ".pdf";
        var path = StagingPath(key);
        var created = false;
        var staged = false;
        try
        {
            long length = 0;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous))
            {
                created = true;
                var buffer = new byte[81920];
                int read;
                while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    length += read;
                    if (length > maximumBytes)
                        throw new InvalidDocumentException($"PDF files must not exceed {maximumBytes / 1024 / 1024} MiB.", tooLarge: true);
                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
                await output.FlushAsync(cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            ValidatePdf(path);
            var stored = new StoredPdf(key, length, Convert.ToHexStringLower(hash.GetHashAndReset()));
            if (publish)
                Promote(stored);
            else
                staged = true;
            return stored;
        }
        finally
        {
            if (created && !staged)
                TryDelete(path);
        }
    }

    internal void Promote(StoredPdf stored)
    {
        File.Move(StagingPath(stored.Key), FinalPath(stored.Key), overwrite: false);
    }

    internal void DeleteStaged(string key)
    {
        TryDelete(StagingPath(key));
    }

    internal Stream OpenRead(string key, long expectedLength)
    {
        FileStream stream;
        try
        {
            stream = new FileStream(FinalPath(key), FileMode.Open, FileAccess.Read, FileShare.Read,
                81920, FileOptions.Asynchronous | FileOptions.RandomAccess);
        }
        catch (IOException exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new ResourceNotFoundException("Document file is unavailable.");
        }
        if (stream.Length != expectedLength)
        {
            stream.Dispose();
            throw new ResourceNotFoundException("Document file is unavailable.");
        }
        return stream;
    }

    internal void Delete(string key)
    {
        TryDelete(FinalPath(key));
    }

    private string StagingPath(string key)
    {
        return Path.Combine(root, ".staging", ValidateKey(key));
    }

    private string FinalPath(string key)
    {
        return Path.Combine(root, ValidateKey(key));
    }

    private static string ValidateKey(string key)
    {
        if (key.Length != 36 || !key.EndsWith(".pdf", StringComparison.Ordinal) ||
            !Guid.TryParseExact(key[..32], "N", out _))
            throw new InvalidOperationException("Invalid private document key.");
        return key;
    }

    private static void ValidatePdf(string path)
    {
        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[5];
        if (stream.Read(header) != header.Length || !header.SequenceEqual("%PDF-"u8))
            throw new InvalidDocumentException("The file must contain a PDF signature.");
        stream.Position = 0;
        try
        {
            using var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
            if (document.SecuritySettings.IsEncrypted || document.PageCount == 0)
                throw new InvalidDocumentException("The PDF must have pages and must not be password-protected.");
            foreach (var page in document.Pages)
                ContentReader.ReadContent(page);
        }
        catch (Exception exception) when (exception is not (InvalidDocumentException or OutOfMemoryException))
        {
            throw new InvalidDocumentException("The PDF is malformed, password-protected, or unsupported.");
        }
    }

    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning("A private document cleanup failed. Check private storage for orphaned files.");
        }
    }
}

internal sealed record StoredPdf(string Key, long ByteLength, string Sha256);
