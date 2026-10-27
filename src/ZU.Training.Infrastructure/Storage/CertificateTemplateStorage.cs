using System.Security.Cryptography;
using ZU.Training.Application.Settings;

namespace ZU.Training.Infrastructure.Storage;

public sealed class CertificateTemplateStorage(string root) : ICertificateTemplateStorage
{
    private const long MaxPdfBytes = 20 * 1024 * 1024;

    public async Task<(string StorageKey, string Sha256)> SavePdfAsync(Stream content,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(root);
        var key = $"{Guid.NewGuid():N}.pdf";
        var path = Path.Combine(root, key);
        try
        {
            await using (var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 81920, FileOptions.Asynchronous))
            {
                var header = new byte[5];
                await content.ReadExactlyAsync(header, cancellationToken);
                if (!header.AsSpan().SequenceEqual("%PDF-"u8))
                    throw new InvalidDataException("The uploaded file is not a PDF.");
                await target.WriteAsync(header, cancellationToken);
                var buffer = new byte[81920];
                long count = header.Length;
                int read;
                while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    count += read;
                    if (count > MaxPdfBytes) throw new InvalidDataException("PDF exceeds 20 MB.");
                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }
            await using var saved = File.OpenRead(path);
            var hash = await SHA256.HashDataAsync(saved, cancellationToken);
            return (key, Convert.ToHexString(hash).ToLowerInvariant());
        }
        catch
        {
            if (File.Exists(path)) File.Delete(path);
            throw;
        }
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(storageKey), "N", out _) ||
            Path.GetExtension(storageKey) != ".pdf" || Path.GetFileName(storageKey) != storageKey)
            return Task.FromResult<Stream?>(null);
        var path = Path.Combine(root, storageKey);
        return Task.FromResult<Stream?>(File.Exists(path) ? File.OpenRead(path) : null);
    }
}
