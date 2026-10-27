using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZU.Training.Application.Administration;
using ZU.Training.Application.Communication;
using ZU.Training.Application.Members;
using ZU.Training.Application.Reporting;

namespace ZU.Training.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController(NotificationService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int take = 30, CancellationToken ct = default)
    {
        var userId = ActorId();
        return Ok(new
        {
            items = await service.ListAsync(userId, take, ct),
            unread = await service.UnreadCountAsync(userId, ct)
        });
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
        => await service.MarkReadAsync(ActorId(), id, ct) ? Ok(new { saved = true }) : NotFound();

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await service.MarkAllReadAsync(ActorId(), ct);
        return Ok(new { saved = true });
    }

    private Guid ActorId() => Guid.TryParse(User.FindFirstValue("sub"), out var id)
        ? id : throw new UnauthorizedAccessException();
}

[ApiController]
[Authorize]
[Route("api/members")]
public sealed class MembersController(MemberDirectoryService service) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> Search([FromQuery] string? query, [FromQuery] Guid? facultyId,
        [FromQuery] Guid? departmentId, [FromQuery] int page = 1, CancellationToken ct = default)
        => Execute(() => service.SearchAsync(ActorId(), query, facultyId, departmentId, page, ct));

    [HttpGet("faculties")]
    public Task<IActionResult> Faculties(CancellationToken ct)
        => Execute(() => service.FacultiesAsync(ActorId(), ct));

    [HttpGet("departments")]
    public Task<IActionResult> Departments([FromQuery] Guid? facultyId, CancellationToken ct)
        => Execute(() => service.DepartmentsAsync(ActorId(), facultyId, ct));

    [HttpGet("me")]
    public Task<IActionResult> Me(CancellationToken ct)
        => Execute(() => service.MyProfileAsync(ActorId(), ct));

    [HttpGet("me/certificates")]
    public Task<IActionResult> MyCertificates(CancellationToken ct)
        => Execute(() => service.MyCertificatesAsync(ActorId(), ct));

    [HttpPut("me")]
    public Task<IActionResult> SaveMe([FromBody] MemberProfileInput input, CancellationToken ct)
        => Execute(async () => { await service.SaveMyProfileAsync(ActorId(), input, ct); return new { saved = true }; });

    [HttpGet("{userId:guid}")]
    public Task<IActionResult> Detail(Guid userId, CancellationToken ct)
        => Execute(() => service.DetailAsync(ActorId(), userId, ct));

    private Guid ActorId() => Guid.TryParse(User.FindFirstValue("sub"), out var id)
        ? id : throw new UnauthorizedAccessException();

    private async Task<IActionResult> Execute<T>(Func<Task<T>> action)
    {
        try { return Ok(await action()); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}

[ApiController]
[Authorize]
[Route("api/statistics")]
public sealed class StatisticsController(StatisticsService service) : ControllerBase
{
    [HttpGet("overview")]
    public Task<IActionResult> Overview([FromQuery] Guid? facultyId, CancellationToken ct)
        => Execute(() => service.OverviewAsync(ActorId(), facultyId, ct));

    [HttpGet("faculties")]
    public Task<IActionResult> Faculties(CancellationToken ct)
        => Execute(() => service.FacultiesAsync(ActorId(), ct));

    [HttpGet("tracks")]
    public Task<IActionResult> Tracks(CancellationToken ct)
        => Execute(() => service.TracksAsync(ActorId(), ct));

    [HttpGet("courses")]
    public Task<IActionResult> Courses([FromQuery] Guid? facultyId, CancellationToken ct)
        => Execute(() => service.CoursesAsync(ActorId(), facultyId, ct));

    [HttpGet("courses/{courseId:guid}")]
    public Task<IActionResult> Course(Guid courseId, CancellationToken ct)
        => Execute(() => service.CourseAsync(ActorId(), courseId, ct));

    [HttpGet("members")]
    public Task<IActionResult> Members([FromQuery] Guid? facultyId, CancellationToken ct)
        => Execute(() => service.MembersAsync(ActorId(), facultyId, ct));

    [HttpGet("evaluation")]
    public Task<IActionResult> Evaluation([FromQuery] Guid? courseId, [FromQuery] Guid? versionId,
        CancellationToken ct)
        => Execute(() => service.EvaluationAsync(ActorId(), courseId, versionId, ct));

    private Guid ActorId() => Guid.TryParse(User.FindFirstValue("sub"), out var id)
        ? id : throw new UnauthorizedAccessException();

    private async Task<IActionResult> Execute<T>(Func<Task<T>> action)
    {
        try { return Ok(await action()); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }
}

[ApiController]
[Authorize]
[Route("api/audit")]
public sealed class AuditController(AuditService service) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> Query([FromQuery] string? action, [FromQuery] string? entityType,
        [FromQuery] Guid? actorId, [FromQuery] DateTimeOffset? fromUtc, [FromQuery] DateTimeOffset? toUtc,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Execute(() => service.QueryAsync(ActorId(), action, entityType, actorId, fromUtc, toUtc, page,
            pageSize, ct));

    [HttpGet("live")]
    public Task<IActionResult> Live([FromQuery] int take = 40, CancellationToken ct = default)
        => Execute(() => service.LiveFeedAsync(ActorId(), take, ct));

    private Guid ActorId() => Guid.TryParse(User.FindFirstValue("sub"), out var id)
        ? id : throw new UnauthorizedAccessException();

    private async Task<IActionResult> Execute<T>(Func<Task<T>> action)
    {
        try { return Ok(await action()); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
