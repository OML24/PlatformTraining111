using ZU.Training.Application.Training;

namespace ZU.Training.Infrastructure.Storage;

/// <summary>تخزين ملفات الشهادات الصادرة (PDF) على القرص خارج قاعدة البيانات.</summary>
public sealed class CertificateDocumentStorage(string root) : ICertificateFileStorage
{
    private const long MaxBytes = 15 * 1024 * 1024;

    public async Task<string> SavePdfAsync(Stream content, CancellationToken ct)
    {
        Directory.CreateDirectory(root);
        var key = Guid.NewGuid().ToString("N") + ".pdf";
        var path = Path.Combine(root, key);
        try
        {
            await using (var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 81920, FileOptions.Asynchronous))
            {
                var buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await content.ReadAsync(buffer, ct)) > 0)
                {
                    total += read;
                    if (total > MaxBytes) throw new InvalidDataException("Certificate PDF exceeds 15 MB.");
                    await target.WriteAsync(buffer.AsMemory(0, read), ct);
                }
                if (total < 200) throw new InvalidDataException("Generated certificate PDF is invalid.");
            }
            return key;
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
        if (string.IsNullOrWhiteSpace(storageKey) || Path.GetFileName(storageKey) != storageKey ||
            Path.GetExtension(storageKey) != ".pdf" ||
            !Guid.TryParseExact(Path.GetFileNameWithoutExtension(storageKey), "N", out _))
            return Task.FromResult<Stream?>(null);
        var path = Path.Combine(root, storageKey);
        return Task.FromResult<Stream?>(File.Exists(path) ? File.OpenRead(path) : null);
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(storageKey) || Path.GetFileName(storageKey) != storageKey) return Task.CompletedTask;
        var path = Path.Combine(root, storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }
}
