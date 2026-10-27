using System.IO.Compression;
using System.Security.Cryptography;
using ZU.Training.Application.Training;

namespace ZU.Training.Infrastructure.Storage;

public sealed class OfficialDocumentStorage(string root) : IOfficialDocumentStorage
{
    private const long MaxBytes = 20 * 1024 * 1024;

    public async Task<StoredOfficialDocument> SaveAsync(Stream content, string originalName, CancellationToken ct)
    {
        var safeName = Path.GetFileName(originalName.Replace('\\', '/'));
        var extension = Path.GetExtension(safeName).ToLowerInvariant();
        if (safeName.Length is < 1 or > 300 || safeName.Any(char.IsControl) ||
            extension is not (".pdf" or ".docx"))
            throw new InvalidDataException("Official document must be a PDF or DOCX.");
        Directory.CreateDirectory(root);
        var key = Guid.NewGuid().ToString("N") + extension;
        var path = Path.Combine(root, key);
        try
        {
            await using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 81920, FileOptions.Asynchronous))
            {
                var bytes = new byte[81920];
                long total = 0; int read;
                while ((read = await content.ReadAsync(bytes, ct)) > 0)
                {
                    total += read;
                    if (total > MaxBytes) throw new InvalidDataException("Document exceeds 20 MB.");
                    await file.WriteAsync(bytes.AsMemory(0, read), ct);
                }
                if (total < 5) throw new InvalidDataException("Document is empty or invalid.");
            }
            await using var saved = File.OpenRead(path);
            var header = new byte[5]; await saved.ReadExactlyAsync(header, ct);
            if (extension == ".pdf" && !header.AsSpan().SequenceEqual("%PDF-"u8))
                throw new InvalidDataException("Invalid PDF.");
            if (extension == ".docx")
            {
                saved.Position = 0;
                using var archive = new ZipArchive(saved, ZipArchiveMode.Read, leaveOpen: true);
                if (archive.GetEntry("[Content_Types].xml") is null || archive.GetEntry("word/document.xml") is null)
                    throw new InvalidDataException("Invalid DOCX.");
            }
            saved.Position = 0;
            var hash = await SHA256.HashDataAsync(saved, ct);
            return new StoredOfficialDocument(key, safeName, Convert.ToHexString(hash).ToLowerInvariant());
        }
        catch
        {
            if (File.Exists(path)) File.Delete(path);
            throw;
        }
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var extension = Path.GetExtension(storageKey);
        if (Path.GetFileName(storageKey) != storageKey || extension is not (".pdf" or ".docx") ||
            !Guid.TryParseExact(Path.GetFileNameWithoutExtension(storageKey), "N", out _))
            return Task.FromResult<Stream?>(null);
        var path = Path.Combine(root, storageKey);
        return Task.FromResult<Stream?>(File.Exists(path) ? File.OpenRead(path) : null);
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var extension = Path.GetExtension(storageKey);
        if (Path.GetFileName(storageKey) != storageKey || extension is not (".pdf" or ".docx") ||
            !Guid.TryParseExact(Path.GetFileNameWithoutExtension(storageKey), "N", out _))
            throw new ArgumentException("Invalid document key.");
        var path = Path.Combine(root, storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }
}
