using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZU.Training.Application.Administration;

namespace ZU.Training.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/external-trainers")]
public sealed class ExternalTrainersController(ExternalTrainerService service) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> List(CancellationToken ct)
        => Execute(() => service.ListAsync(ActorId(), ct));

    [HttpGet("suggest-login")]
    [Authorize(Policy = ZU.Training.Domain.Identity.PermissionKeys.ManageUsers)]
    public IActionResult SuggestLogin() => Ok(new { login = ExternalTrainerService.SuggestLogin() });

    [HttpPost]
    public Task<IActionResult> Request([FromBody] ExternalTrainerRequestInput input, CancellationToken ct)
        => Execute(async () => await service.RequestAsync(ActorId(), input, Ip(), UserAgent(), ct));

    [HttpPost("{id:guid}/account")]
    [Authorize(Policy = ZU.Training.Domain.Identity.PermissionKeys.ManageUsers)]
    public Task<IActionResult> CreateAccount(Guid id, [FromBody] CreateExternalAccountInput input,
        CancellationToken ct)
        => Execute(() => service.CreateAccountAsync(ActorId(), id, input, Ip(), UserAgent(), ct));

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = ZU.Training.Domain.Identity.PermissionKeys.ManageUsers)]
    public Task<IActionResult> Reject(Guid id, [FromBody] RejectRequest request, CancellationToken ct)
        => Execute(() => service.RejectAsync(ActorId(), id, request.NoteAr, Ip(), UserAgent(), ct));

    private Guid ActorId() => Guid.TryParse(User.FindFirstValue("sub"), out var id)
        ? id : throw new UnauthorizedAccessException();

    private string? Ip() => HttpContext.Connection.RemoteIpAddress?.ToString();

    private string? UserAgent()
    {
        var value = Request.Headers.UserAgent.ToString();
        return string.IsNullOrWhiteSpace(value) ? null : (value.Length > 400 ? value[..400] : value);
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

public sealed record RejectRequest(string? NoteAr);
