using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZU.Training.Application.Training;
using ZU.Training.Domain.Training;

namespace ZU.Training.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/courses")]
public sealed class CoursesController(CourseWorkflowService workflow, CertificateGateService certificateGate,
    QualityRequestService qualityRequests) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> List(CancellationToken ct)
        => Execute(() => workflow.ListAsync(CurrentUserId(), ct));

    [HttpGet("tracks")]
    public Task<IActionResult> Tracks(CancellationToken ct)
        => Execute(() => workflow.TracksAsync(CurrentUserId(), ct));

    [HttpGet("faculties")]
    public Task<IActionResult> Faculties(CancellationToken ct)
        => Execute(() => workflow.FacultiesAsync(CurrentUserId(), ct));

    [HttpGet("directory-users")]
    public Task<IActionResult> SearchUsers([FromQuery] string query, [FromQuery] Guid? facultyId,
        CancellationToken ct)
        => Execute(() => workflow.SearchUsersAsync(CurrentUserId(), query, facultyId, ct));

    [HttpGet("{courseId:guid}")]
    public Task<IActionResult> Details(Guid courseId, CancellationToken ct)
        => Execute(() => workflow.DetailsAsync(CurrentUserId(), courseId, ct));

    [HttpGet("{courseId:guid}/certificate-eligibility")]
    public Task<IActionResult> CertificateEligibility(Guid courseId, CancellationToken ct)
        => Execute(() => certificateGate.StatusAsync(CurrentUserId(), courseId, ct));

    [HttpPost]
    public Task<IActionResult> Create([FromBody] CreateCourseInput input, CancellationToken ct)
        => Execute(() => workflow.CreateAsync(CurrentUserId(), input, ct));

    [HttpPost("{courseId:guid}/approve")]
    public Task<IActionResult> Approve(Guid courseId, [FromBody] ApproveCourseRequest request, CancellationToken ct)
        => Execute(() => workflow.ApproveAsync(CurrentUserId(), courseId, request.EvaluationDeadlineUtc,
            request.QualityOperated, request.InstructorUserId, ct));

    [HttpPost("{courseId:guid}/review")]
    public Task<IActionResult> ReviewProposal(Guid courseId, [FromBody] CourseProposalReviewRequest request,
        CancellationToken ct)
        => Execute(() => workflow.ReviewProposalAsync(CurrentUserId(), courseId,
            request.Decision, request.NoteAr, ct));

    [HttpPut("{courseId:guid}/resubmit")]
    public Task<IActionResult> ResubmitProposal(Guid courseId, [FromBody] CreateCourseInput input,
        CancellationToken ct)
        => Execute(() => workflow.ResubmitProposalAsync(CurrentUserId(), courseId, input, ct));

    [HttpPost("{courseId:guid}/enroll")]
    public Task<IActionResult> SelfEnroll(Guid courseId, CancellationToken ct)
        => Execute(() => workflow.SelfEnrollAsync(CurrentUserId(), courseId, ct));

    [HttpPut("{courseId:guid}/evaluation-deadline")]
    public Task<IActionResult> ExtendEvaluation(Guid courseId, [FromBody] EvaluationDeadlineRequest request,
        CancellationToken ct)
        => Execute(() => workflow.ExtendEvaluationAsync(CurrentUserId(), courseId, request.DeadlineUtc, ct));

    [HttpPost("{courseId:guid}/participants")]
    public Task<IActionResult> AddParticipant(Guid courseId, [FromBody] ParticipantRequest request, CancellationToken ct)
        => Execute(() => workflow.SelectParticipantAsync(CurrentUserId(), courseId, request.UserId, ct));

    [HttpGet("{courseId:guid}/documents")]
    public Task<IActionResult> Documents(Guid courseId, CancellationToken ct)
        => Execute(() => qualityRequests.CourseDocumentsAsync(CurrentUserId(), courseId, ct));

    /// <summary>إرفاق الكتاب الرسمي أو السيرة الذاتية لمحاضر خارجي في مقترح الدورة.</summary>
    [HttpPost("{courseId:guid}/documents")]
    [RequestSizeLimit(21 * 1024 * 1024)]
    public async Task<IActionResult> AttachDocument(Guid courseId, [FromQuery] string kind, IFormFile? file,
        CancellationToken ct)
    {
        if (file is null || file.Length is < 5 or > 20 * 1024 * 1024)
            return BadRequest(new { message = "A PDF or DOCX up to 20 MB is required." });
        return await Execute(async () =>
        {
            await using var stream = file.OpenReadStream();
            return await qualityRequests.AttachCourseDocumentAsync(CurrentUserId(), courseId, kind, stream,
                file.FileName, ct);
        });
    }

    [HttpPost("{courseId:guid}/lectures")]
    public Task<IActionResult> AddLecture(Guid courseId, [FromBody] NamedRecordRequest request, CancellationToken ct)
        => Execute(() => workflow.AddLectureAsync(CurrentUserId(), courseId, request.NameAr, request.NameEn,
            request.StartsAtUtc, request.Mode, request.OnlineLink, ct));

    [HttpPut("lectures/{lectureId:guid}")]
    public Task<IActionResult> UpdateLecture(Guid lectureId, [FromBody] LectureUpdateRequest request,
        CancellationToken ct)
        => Execute(() => workflow.UpdateLectureAsync(CurrentUserId(), lectureId, request.StartsAtUtc,
            request.Mode, request.OnlineLink, ct));

    [HttpGet("lectures/{lectureId:guid}/materials")]
    public Task<IActionResult> Materials(Guid lectureId, CancellationToken ct)
        => Execute(() => workflow.MaterialsAsync(CurrentUserId(), lectureId, ct));

    [HttpPost("lectures/{lectureId:guid}/materials")]
    [RequestSizeLimit(41 * 1024 * 1024)]
    public async Task<IActionResult> UploadMaterial(Guid lectureId, IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length is < 5 or > 40 * 1024 * 1024)
            return BadRequest(new { message = "A file of up to 40 MB is required." });
        return await Execute(async () =>
        {
            await using var stream = file.OpenReadStream();
            return await workflow.AddMaterialAsync(CurrentUserId(), lectureId, stream, file.FileName, ct);
        });
    }

    [HttpGet("materials/{materialId:guid}/file")]
    public async Task<IActionResult> DownloadMaterial(Guid materialId, CancellationToken ct)
    {
        try
        {
            var file = await workflow.OpenMaterialAsync(CurrentUserId(), materialId, ct);
            Response.Headers.CacheControl = "private, max-age=300";
            return File(file.Content, file.ContentType, file.FileName);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost("historical")]
    public Task<IActionResult> CreateHistorical([FromBody] HistoricalCourseInput input, CancellationToken ct)
        => Execute(() => workflow.CreateHistoricalAsync(CurrentUserId(), input, ct));

    [HttpPost("lectures/{lectureId:guid}/activate-code")]
    public async Task<IActionResult> ActivateCode(Guid lectureId, CancellationToken ct)
    {
        try
        {
            var lecture = await workflow.ActivateCodeAsync(CurrentUserId(), lectureId, ct);
            return Ok(new { lecture.AttendanceCode, ExpiresAtUtc = lecture.CodeActivatedAtUtc?.AddMinutes(5) });
        }
        catch (Exception ex) when (IsBusinessError(ex)) { return BusinessError(ex); }
    }

    [HttpGet("lectures/{lectureId:guid}/code")]
    public async Task<IActionResult> Code(Guid lectureId, CancellationToken ct)
    {
        try
        {
            var lecture = await workflow.OperatorLectureAsync(CurrentUserId(), lectureId, ct);
            return Ok(new { lecture.AttendanceCode, ExpiresAtUtc = lecture.CodeActivatedAtUtc?.AddMinutes(5) });
        }
        catch (Exception ex) when (IsBusinessError(ex)) { return BusinessError(ex); }
    }

    [HttpPost("lectures/{lectureId:guid}/attendance-code")]
    public Task<IActionResult> EnterCode(Guid lectureId, [FromBody] AttendanceCodeRequest request, CancellationToken ct)
        => Execute(() => workflow.EnterCodeAsync(CurrentUserId(), lectureId, request.Code, ct));

    [HttpPut("lectures/{lectureId:guid}/attendance/{enrollmentId:guid}")]
    public Task<IActionResult> SetAttendance(Guid lectureId, Guid enrollmentId,
        [FromBody] ManualAttendanceRequest request, CancellationToken ct)
        => Execute(() => workflow.SetAttendanceAsync(CurrentUserId(), lectureId, enrollmentId, request.IsPresent, ct));

    [HttpPost("{courseId:guid}/end")]
    public Task<IActionResult> End(Guid courseId, CancellationToken ct)
        => Execute(() => workflow.EndAsync(CurrentUserId(), courseId, ct));

    [HttpPost("{courseId:guid}/exam")]
    public Task<IActionResult> CreateExam(Guid courseId, [FromBody] NamedRecordRequest request, CancellationToken ct)
        => Execute(() => workflow.CreateExamAsync(CurrentUserId(), courseId, request.NameAr, request.NameEn, ct));

    [HttpPut("{courseId:guid}/exam-requirement")]
    public Task<IActionResult> ChangeExamRequirement(Guid courseId,
        [FromBody] ExamRequirementRequest request, CancellationToken ct)
        => Execute(() => workflow.ChangeExamRequirementAsync(CurrentUserId(), courseId, request.Required, ct));

    [HttpPut("{courseId:guid}/exam/results/{enrollmentId:guid}")]
    public Task<IActionResult> ExamResult(Guid courseId, Guid enrollmentId,
        [FromBody] ExamResultRequest request, CancellationToken ct)
        => Execute(() => workflow.RecordExamResultAsync(CurrentUserId(), courseId, enrollmentId, request.Outcome, ct));

    private Guid CurrentUserId()
        => Guid.TryParse(User.FindFirstValue("sub"), out var id) ? id : throw new UnauthorizedAccessException();

    private async Task<IActionResult> Execute<T>(Func<Task<T>> action)
    {
        try { return Ok(await action()); }
        catch (Exception ex) when (IsBusinessError(ex)) { return BusinessError(ex); }
    }

    private static bool IsBusinessError(Exception ex)
        => ex is ArgumentException or InvalidOperationException or KeyNotFoundException or UnauthorizedAccessException;

    private IActionResult BusinessError(Exception ex) => ex switch
    {
        UnauthorizedAccessException => Forbid(),
        KeyNotFoundException => NotFound(new { message = ex.Message }),
        _ => BadRequest(new { message = ex.Message })
    };
}

public sealed record ApproveCourseRequest(DateTimeOffset EvaluationDeadlineUtc,
    bool QualityOperated, Guid? InstructorUserId);
public sealed record ParticipantRequest(Guid UserId);
public sealed record NamedRecordRequest(string NameAr, string? NameEn, DateTimeOffset? StartsAtUtc,
    DeliveryMode Mode = DeliveryMode.InPerson, string? OnlineLink = null);
public sealed record LectureUpdateRequest(DateTimeOffset? StartsAtUtc, DeliveryMode Mode, string? OnlineLink);
public sealed record AttendanceCodeRequest(string Code);
public sealed record ManualAttendanceRequest(bool IsPresent);
public sealed record ExamResultRequest(ExamOutcome Outcome);
public sealed record EvaluationDeadlineRequest(DateTimeOffset DeadlineUtc);
public sealed record ExamRequirementRequest(bool Required);
public sealed record CourseProposalReviewRequest(CourseStatus Decision, string NoteAr);
