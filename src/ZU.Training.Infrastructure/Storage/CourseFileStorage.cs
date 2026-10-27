using System.IO.Compression;
using System.Security.Cryptography;
using ZU.Training.Application.Training;

namespace ZU.Training.Infrastructure.Storage;

/// <summary>تخزين ملفات ومرفقات الدورات والمحاضرات (حقائب تدريبية، عروض، ملفات).</summary>
public sealed class CourseFileStorage(string root) : ICourseFileStorage
{
    private const long MaxBytes = 40 * 1024 * 1024;
    private static readonly string[] Allowed = [".pdf", ".docx", ".pptx", ".xlsx", ".zip", ".txt"];

    public async Task<StoredCourseFile> SaveAsync(Stream content, string originalName, CancellationToken ct)
    {
        var safeName = Path.GetFileName((originalName ?? string.Empty).Replace('\\', '/'));
        var extension = Path.GetExtension(safeName).ToLowerInvariant();
        if (safeName.Length is < 1 or > 300 || safeName.Any(char.IsControl) || !Allowed.Contains(extension))
            throw new InvalidDataException("Unsupported file type. Allowed: PDF, DOCX, PPTX, XLSX, ZIP, TXT.");

        Directory.CreateDirectory(root);
        var key = Guid.NewGuid().ToString("N") + extension;
        var path = Path.Combine(root, key);
        try
        {
            await using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 81920, FileOptions.Asynchronous))
            {
                var buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await content.ReadAsync(buffer, ct)) > 0)
                {
                    total += read;
                    if (total > MaxBytes) throw new InvalidDataException("File exceeds 40 MB.");
                    await file.WriteAsync(buffer.AsMemory(0, read), ct);
                }
                if (total < 5) throw new InvalidDataException("File is empty or invalid.");
            }

            await using var saved = File.OpenRead(path);
            if (extension == ".pdf")
            {
                var header = new byte[5];
                await saved.ReadExactlyAsync(header, ct);
                if (!header.AsSpan().SequenceEqual("%PDF-"u8)) throw new InvalidDataException("Invalid PDF.");
            }
            else if (extension is ".docx" or ".pptx" or ".xlsx" or ".zip")
            {
                using var archive = new ZipArchive(saved, ZipArchiveMode.Read, leaveOpen: true);
                if (extension == ".docx" && (archive.GetEntry("[Content_Types].xml") is null ||
                    archive.GetEntry("word/document.xml") is null))
                    throw new InvalidDataException("Invalid DOCX.");
                if (extension == ".pptx" && (archive.GetEntry("[Content_Types].xml") is null ||
                    archive.GetEntry("ppt/presentation.xml") is null))
                    throw new InvalidDataException("Invalid PPTX.");
                if (extension == ".xlsx" && (archive.GetEntry("[Content_Types].xml") is null ||
                    archive.GetEntry("xl/workbook.xml") is null))
                    throw new InvalidDataException("Invalid XLSX.");
            }

            saved.Position = 0;
            var hash = await SHA256.HashDataAsync(saved, ct);
            return new StoredCourseFile(key, safeName, Convert.ToHexStringLower(hash));
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
        var extension = Path.GetExtension(storageKey ?? string.Empty);
        if (string.IsNullOrWhiteSpace(storageKey) || Path.GetFileName(storageKey) != storageKey ||
            !Allowed.Contains(extension) ||
            !Guid.TryParseExact(Path.GetFileNameWithoutExtension(storageKey), "N", out _))
            return Task.FromResult<Stream?>(null);
        var path = Path.Combine(root, storageKey);
        return Task.FromResult<Stream?>(File.Exists(path) ? File.OpenRead(path) : null);
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var extension = Path.GetExtension(storageKey ?? string.Empty);
        if (string.IsNullOrWhiteSpace(storageKey) || Path.GetFileName(storageKey) != storageKey ||
            !Allowed.Contains(extension))
            throw new ArgumentException("Invalid file key.");
        var path = Path.Combine(root, storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }
}
