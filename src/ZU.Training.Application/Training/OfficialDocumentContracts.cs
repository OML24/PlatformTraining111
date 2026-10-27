namespace ZU.Training.Application.Training;

public sealed record StoredOfficialDocument(string StorageKey, string OriginalName, string Sha256);

public interface IOfficialDocumentStorage
{
    Task<StoredOfficialDocument> SaveAsync(Stream content, string originalName,
        CancellationToken cancellationToken);
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}

public sealed record StoredCourseFile(string StorageKey, string OriginalName, string Sha256);

/// <summary>تخزين ملفات الدورات (حقائب تدريبية، عروض، مرفقات محاضرات).</summary>
public interface ICourseFileStorage
{
    Task<StoredCourseFile> SaveAsync(Stream content, string originalName, CancellationToken ct);
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct);
    Task DeleteAsync(string storageKey, CancellationToken ct);
}

public sealed record LectureMaterialView(Guid Id, Guid LectureId, string OriginalName, string Sha256,
    DateTimeOffset UploadedAtUtc);

public sealed record CourseDownload(Stream Content, string FileName, string ContentType);
