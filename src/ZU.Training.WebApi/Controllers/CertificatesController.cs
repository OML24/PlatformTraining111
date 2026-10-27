using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZU.Training.Application.Training;

namespace ZU.Training.WebApi.Controllers;

[ApiController]
[Route("api/certificates")]
public sealed class CertificatesController(CertificateVerificationService verification,
    CertificateIssuanceService issuance, ICertificateVerificationStore verificationStore,
    IQrCodeRenderer qrRenderer, CertificateOptions options) : ControllerBase
{
    /// <summary>رمز QR لرابط التحقق الرسمي، يُستخدم في صفحة التحقق وبطاقات الشهادات.</summary>
    [HttpGet("qr")]
    [AllowAnonymous]
    public async Task<IActionResult> Qr([FromQuery] string? cert, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cert)) return BadRequest(new { message = "Certificate reference is required." });
        var certificate = await verificationStore.ByReferenceAsync(cert.Trim(), ct);
        if (certificate is null || certificate.Status == Domain.Training.CertificateStatus.Hidden) return NotFound();
        var png = qrRenderer.RenderPng(options.UrlFor(certificate.ReferenceNumber));
        Response.Headers.CacheControl = "public, max-age=86400";
        return File(png, "image/png");
    }

    [HttpGet("verify")]
    [AllowAnonymous]
    public async Task<IActionResult> Verify([FromQuery] string? cert, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cert)) return BadRequest(new { message = "Certificate reference is required." });
        try { return Ok(await verification.VerifyAsync(cert, ct)); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpGet("mine")]
    [Authorize]
    public Task<IActionResult> Mine(CancellationToken ct)
        => Execute(() => issuance.MineAsync(ActorId(), ct));

    [HttpGet("members/{userId:guid}")]
    [Authorize]
    public Task<IActionResult> ForMember(Guid userId, CancellationToken ct)
        => Execute(() => issuance.ProfileAsync(ActorId(), userId, ct));

    [HttpGet("courses/{courseId:guid}")]
    [Authorize]
    public Task<IActionResult> ForCourse(Guid courseId, CancellationToken ct)
        => Execute(() => issuance.ForCourseAsync(ActorId(), courseId, ct));

    [HttpPost("courses/{courseId:guid}/issue")]
    [Authorize]
    public Task<IActionResult> IssueForCourse(Guid courseId, CancellationToken ct)
        => Execute(async () => new { issued = await issuance.IssueForCourseAsync(ActorId(), courseId, ct) });

    [HttpPost("{certificateId:guid}/revoke")]
    [Authorize]
    public Task<IActionResult> Revoke(Guid certificateId, [FromBody] RevokeCertificateRequest request,
        CancellationToken ct)
        => Execute(async () =>
        {
            await issuance.RevokeAsync(ActorId(), certificateId, request.Reason, ct);
            return new { revoked = true };
        });

    [HttpGet("{certificateId:guid}/file")]
    [Authorize]
    public async Task<IActionResult> Download(Guid certificateId, CancellationToken ct)
    {
        try
        {
            var file = await issuance.DownloadAsync(ActorId(), certificateId, ct);
            Response.Headers.CacheControl = "no-store";
            return File(file.Content, "application/pdf", file.FileName);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    private Guid ActorId() => Guid.TryParse(User.FindFirstValue("sub"), out var id)
        ? id : throw new UnauthorizedAccessException();

    private async Task<IActionResult> Execute<T>(Func<Task<T>> action)
    {
        try { return Ok(await action()); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ArgumentNullException ex) { return BadRequest(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}

public sealed record RevokeCertificateRequest(string Reason);
