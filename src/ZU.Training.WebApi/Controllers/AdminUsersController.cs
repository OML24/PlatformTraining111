using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ZU.Training.Application.Administration;
using ZU.Training.Domain.Identity;

namespace ZU.Training.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/admin")]
public sealed class AdminUsersController(UserAdminService service, CredentialEmailService emails)
    : ControllerBase
{
    [HttpGet("users")]
    [Authorize(Policy = PermissionKeys.ManageUsers)]
    public Task<IActionResult> Users([FromQuery] string? query, [FromQuery] int page = 1, CancellationToken ct = default)
        => Execute(() => service.SearchAsync(query ?? "", page, ct));

    [HttpGet("roles")]
    [Authorize(Policy = PermissionKeys.ManageUsers)]
    public Task<IActionResult> Roles(CancellationToken ct)
        => Execute(async () => (await service.RolesAsync(ct)).Select(x =>
            new { x.Key, x.NameAr, x.NameEn }).ToList());

    [HttpGet("roles/{roleKey}/permissions")]
    [Authorize(Policy = PermissionKeys.ManagePermissions)]
    public Task<IActionResult> RoleMatrix(string roleKey, CancellationToken ct)
        => Execute(() => service.RoleMatrixAsync(roleKey, ct));

    [HttpPut("roles/{roleKey}/permissions/{permissionKey}")]
    [Authorize(Policy = PermissionKeys.ManagePermissions)]
    public Task<IActionResult> SetRolePermission(string roleKey, string permissionKey,
        [FromBody] RolePermissionChangeRequest request, CancellationToken ct)
        => Execute(async () => { await service.SetRolePermissionAsync(ActorId(), roleKey, permissionKey,
            request.Granted, Ip(), ct); return new { saved = true }; });

    [HttpPost("users")]
    [Authorize(Policy = PermissionKeys.ManageUsers)]
    public Task<IActionResult> CreateUser([FromBody] CreateUserInput input, CancellationToken ct)
        => Execute(() => service.CreateAsync(ActorId(), input, Ip(), ct));

    [HttpGet("users/{id:guid}/credentials")]
    [Authorize(Policy = PermissionKeys.ManageUsers)]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Credentials(Guid id, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return await Execute(() => service.CredentialsAsync(ActorId(), id, Ip(), ct));
    }

    [HttpPut("users/{id:guid}/password")]
    [Authorize(Policy = PermissionKeys.ManageUsers)]
    [EnableRateLimiting("auth")]
    public Task<IActionResult> Password(Guid id, [FromBody] PasswordChangeRequest request, CancellationToken ct)
        => Execute(async () => { await service.ChangePasswordAsync(ActorId(), id, request.Password, Ip(), ct); return new { saved = true }; });

    [HttpPut("users/{id:guid}/active")]
    [Authorize(Policy = PermissionKeys.ManageUsers)]
    public Task<IActionResult> Active(Guid id, [FromBody] ActiveChangeRequest request, CancellationToken ct)
        => Execute(async () => { await service.SetActiveAsync(ActorId(), id, request.IsActive, Ip(), ct); return new { saved = true }; });

    [HttpPut("users/{id:guid}/access-override")]
    [Authorize(Policy = PermissionKeys.ManageUsers)]
    public Task<IActionResult> AccessOverride(Guid id, [FromBody] AccessOverrideRequest request, CancellationToken ct)
        => Execute(async () => { await service.SetAccessOverrideAsync(ActorId(), id, request.Allowed, Ip(), ct); return new { saved = true }; });

    [HttpPut("users/{id:guid}/roles")]
    [Authorize(Policy = PermissionKeys.ManagePermissions)]
    public Task<IActionResult> SetRoles(Guid id, [FromBody] RoleKeysRequest request, CancellationToken ct)
        => Execute(async () => { await service.SetRolesAsync(ActorId(), id, request.RoleKeys, Ip(), ct); return new { saved = true }; });

    [HttpGet("users/{id:guid}/permissions")]
    [Authorize(Policy = PermissionKeys.ManagePermissions)]
    public Task<IActionResult> PermissionMatrix(Guid id, CancellationToken ct)
        => Execute(() => service.PermissionMatrixAsync(id, ct));

    [HttpPut("users/{id:guid}/permissions/{key}")]
    [Authorize(Policy = PermissionKeys.ManagePermissions)]
    public Task<IActionResult> SetPermissionOverride(Guid id, string key, [FromBody] PermissionOverrideRequest request,
        CancellationToken ct)
        => Execute(async () => { await service.SetPermissionOverrideAsync(ActorId(), id, key, request.Mode, Ip(), ct); return new { saved = true }; });

    [HttpGet("shared-initial-password")]
    [Authorize(Policy = PermissionKeys.ManageUsers)]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> SharedInitialPassword(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return await Execute(async () => new { password = await service.SharedInitialPasswordAsync(ActorId(), Ip(), ct) });
    }

    [HttpPut("shared-initial-password")]
    [Authorize(Policy = PermissionKeys.ManageUsers)]
    [EnableRateLimiting("auth")]
    public Task<IActionResult> SetSharedInitialPassword([FromBody] PasswordChangeRequest request, CancellationToken ct)
        => Execute(async () => { await service.SetSharedInitialPasswordAsync(ActorId(), request.Password, Ip(), ct); return new { saved = true }; });

    [HttpGet("credentials-email")]
    [Authorize(Policy = PermissionKeys.SendCredentialEmails)]
    public Task<IActionResult> CredentialEmailStatus(CancellationToken ct)
        => Execute(() => emails.StatusAsync(ct));

    [HttpPost("credentials-email/send")]
    [Authorize(Policy = PermissionKeys.SendCredentialEmails)]
    [EnableRateLimiting("auth")]
    public Task<IActionResult> SendCredentialEmails(CancellationToken ct)
        => Execute(() => emails.SendPendingAsync(ActorId(), Ip(), UserAgent(), ct));

    [HttpPost("users/{id:guid}/send-credentials")]
    [Authorize(Policy = PermissionKeys.SendCredentialEmails)]
    [EnableRateLimiting("auth")]
    public Task<IActionResult> SendUserCredentials(Guid id, CancellationToken ct)
        => Execute(() => emails.SendToUserAsync(ActorId(), id, Ip(), UserAgent(), ct));

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
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }
}

public sealed record PasswordChangeRequest(string Password);
public sealed record ActiveChangeRequest(bool IsActive);
public sealed record AccessOverrideRequest(bool Allowed);
public sealed record RoleKeysRequest(IReadOnlyList<string> RoleKeys);
public sealed record PermissionOverrideRequest(PermissionOverrideMode Mode);
public sealed record RolePermissionChangeRequest(bool Granted);
