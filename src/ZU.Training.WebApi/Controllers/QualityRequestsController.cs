using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZU.Training.Application.Training;

namespace ZU.Training.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/training-needs")]
public sealed class TrainingNeedsController(QualityRequestService service) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> List(CancellationToken ct)
        => Execute(() => service.NeedsAsync(ActorId(), ct));

    [HttpPost]
    [RequestSizeLimit(21 * 1024 * 1024)]
    public async Task<IActionResult> Submit([FromForm] string titleAr, [FromForm] string? titleEn,
        [FromForm] string descriptionAr, [FromForm] string? descriptionEn, [FromForm] Guid? facultyId,
        IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length is < 5 or > 20 * 1024 * 1024)
            return BadRequest(new { message = "An official PDF or DOCX up to 20 MB is required." });
        return await Execute(async () =>
        {
            await using var stream = file.OpenReadStream();
            var need = await service.SubmitNeedAsync(ActorId(),
                new TrainingNeedInput(titleAr, titleEn, descriptionAr, descriptionEn, facultyId),
                stream, file.FileName, ct);
            return new { need.Id, need.TitleAr, need.Status };
        });
    }

    [HttpPut("{id:guid}/resubmit")]
    [RequestSizeLimit(21 * 1024 * 1024)]
    public async Task<IActionResult> Resubmit(Guid id, [FromForm] string titleAr, [FromForm] string? titleEn,
        [FromForm] string descriptionAr, [FromForm] string? descriptionEn, IFormFile? file, CancellationToken ct)
    {
        if (file?.Length > 20 * 1024 * 1024) return BadRequest(new { message = "Document exceeds 20 MB." });
        return await Execute(async () =>
        {
            await using var stream = file?.OpenReadStream();
            var need = await service.ResubmitNeedAsync(ActorId(), id,
                new TrainingNeedInput(titleAr, titleEn, descriptionAr, descriptionEn, null),
                stream, file?.FileName, ct);
            return new { need.Id, need.TitleAr, need.Status };
        });
    }

    [HttpPut("{id:guid}/review")]
    public Task<IActionResult> Review(Guid id, [FromBody] ReviewRequestInput input, CancellationToken ct)
        => Execute(async () =>
        {
            var need = await service.ReviewNeedAsync(ActorId(), id, input, ct);
            return new { need.Id, need.Status, need.ReviewNoteAr };
        });

    private Guid ActorId() => Guid.TryParse(User.FindFirstValue("sub"), out var id)
        ? id : throw new UnauthorizedAccessException();

    private async Task<IActionResult> Execute<T>(Func<Task<T>> action)
    {
        try { return Ok(await action()); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidDataException ex) { return BadRequest(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}

[ApiController]
[Authorize]
[Route("api/course-cancellations")]
public sealed class CourseCancellationsController(QualityRequestService service) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> List(CancellationToken ct)
        => Execute(() => service.CancellationsAsync(ActorId(), ct));

    [HttpPost("{courseId:guid}")]
    [RequestSizeLimit(21 * 1024 * 1024)]
    public async Task<IActionResult> Submit(Guid courseId, [FromForm] string reasonAr,
        IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length is < 5 or > 20 * 1024 * 1024)
            return BadRequest(new { message = "An official PDF or DOCX up to 20 MB is required." });
        return await Execute(async () =>
        {
            await using var stream = file.OpenReadStream();
            var request = await service.RequestCancellationAsync(ActorId(), courseId,
                new CancellationInput(reasonAr), stream, file.FileName, ct);
            return new { request.Id, request.CourseId, request.Status };
        });
    }

    [HttpPut("{id:guid}/review")]
    public Task<IActionResult> Review(Guid id, [FromBody] ReviewRequestInput input, CancellationToken ct)
        => Execute(async () =>
        {
            var request = await service.ReviewCancellationAsync(ActorId(), id, input, ct);
            return new { request.Id, request.CourseId, request.Status, request.ReviewNoteAr };
        });

    private Guid ActorId() => Guid.TryParse(User.FindFirstValue("sub"), out var id)
        ? id : throw new UnauthorizedAccessException();

    private async Task<IActionResult> Execute<T>(Func<Task<T>> action)
    {
        try { return Ok(await action()); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidDataException ex) { return BadRequest(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}

[ApiController]
[Authorize]
[Route("api/official-documents")]
public sealed class OfficialDocumentsController(QualityRequestService service) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> Vault([FromQuery] string? query, [FromQuery] string? kind,
        [FromQuery] Guid? entityId, [FromQuery] int page = 1, CancellationToken ct = default)
        => Execute(() => service.VaultAsync(ActorId(), query, kind, entityId, page, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId)) return Unauthorized();
        try
        {
            var document = await service.OpenDocumentAsync(actorId, id, ct);
            Response.Headers.CacheControl = "no-store";
            var contentType = document.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                ? "application/pdf" : "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
            return File(document.Content, contentType, document.FileName);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    private async Task<IActionResult> Execute<T>(Func<Task<T>> action)
    {
        try { return Ok(await action()); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}
