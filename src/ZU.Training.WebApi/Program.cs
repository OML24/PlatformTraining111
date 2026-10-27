using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ZU.Training.Application.Communication;
using ZU.Training.Application.Members;
using ZU.Training.Application.Reporting;
using ZU.Training.Application.Security;
using ZU.Training.Application.Administration;
using ZU.Training.Application.Integrations;
using ZU.Training.Application.Settings;
using ZU.Training.Application.Training;
using ZU.Training.Domain.Identity;
using ZU.Training.Infrastructure.Data;
using ZU.Training.Infrastructure.Repositories;
using ZU.Training.Infrastructure.Security;
using ZU.Training.Infrastructure.Storage;
using ZU.Training.Infrastructure.Integrations;
using ZU.Training.Infrastructure.Communication;
using ZU.Training.Infrastructure.Documents;
using ZU.Training.WebApi.Security;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("TrainingDb")
    ?? throw new InvalidOperationException("ConnectionStrings:TrainingDb must be configured outside source control.");
var encryptionKey = builder.Configuration["Security:EncryptionKey"]
    ?? throw new InvalidOperationException("Security:EncryptionKey must be configured outside source control.");
var jwtKey = builder.Configuration["Security:JwtSigningKey"]
    ?? throw new InvalidOperationException("Security:JwtSigningKey must be configured outside source control.");
var portalOrigin = builder.Configuration["WebPortal:Origin"]
    ?? throw new InvalidOperationException("WebPortal:Origin must be configured.");

static string StoragePath(IConfiguration configuration, string key, string fallbackRoot, string folder)
{
    var configured = configuration[key];
    return string.IsNullOrWhiteSpace(configured) ? Path.Combine(fallbackRoot, "storage", folder) : configured;
}

builder.Services.AddDbContext<TrainingDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<IAuthStore, EfAuthStore>();
builder.Services.AddScoped<IPermissionAccess, EfPermissionAccess>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ISettingsStore, EfSettingsStore>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<ICourseStore, EfCourseStore>();
builder.Services.AddScoped<CourseWorkflowService>();
builder.Services.AddScoped<IQualityRequestStore, EfQualityRequestStore>();
builder.Services.AddScoped<QualityRequestService>();
builder.Services.AddScoped<IImpactStore, EfImpactStore>();
builder.Services.AddScoped<ImpactService>();
builder.Services.AddScoped<IEvaluationStore, EfEvaluationStore>();
builder.Services.AddScoped<EvaluationService>();
builder.Services.AddScoped<ICertificateGateStore, EfCertificateGateStore>();
builder.Services.AddScoped<CertificateGateService>();
builder.Services.AddScoped<CertificateIssuanceService>();
builder.Services.AddScoped<ICertificateVerificationStore, EfCertificateVerificationStore>();
builder.Services.AddScoped<CertificateVerificationService>();
builder.Services.AddScoped<INotificationStore, EfNotificationStore>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<ICertificateIssuanceStore, EfCertificateIssuanceStore>();
builder.Services.AddScoped<IAuditStore, EfAuditStore>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<IMemberDirectoryStore, EfMemberDirectoryStore>();
builder.Services.AddScoped<MemberDirectoryService>();
builder.Services.AddScoped<IStatisticsStore, EfStatisticsStore>();
builder.Services.AddScoped<StatisticsService>();
builder.Services.AddScoped<IExternalTrainerStore, EfExternalTrainerStore>();
builder.Services.AddScoped<ExternalTrainerService>();
builder.Services.AddScoped<PasswordRecoveryService>();
builder.Services.AddScoped<CredentialEmailService>();
builder.Services.AddScoped<ICourseAdminStore, EfCourseAdminStore>();
builder.Services.AddScoped<CourseAdminService>();
builder.Services.AddSingleton<IOfficialDocumentStorage>(new OfficialDocumentStorage(StoragePath(
    builder.Configuration, "Storage:OfficialDocumentsPath", builder.Environment.ContentRootPath,
    "official-documents")));
builder.Services.AddScoped<IUserAdminStore, EfUserAdminStore>();
builder.Services.AddScoped<UserAdminService>();
builder.Services.AddSingleton(new UniversityDirectoryEndpoints(
    builder.Configuration["University:Employees"] ?? throw new InvalidOperationException("University employee URL missing."),
    builder.Configuration["University:Departments"] ?? throw new InvalidOperationException("University department URL missing."),
    builder.Configuration["University:Faculties"] ?? throw new InvalidOperationException("University faculty URL missing.")));
builder.Services.AddHttpClient<IUniversitySyncService, UniversitySyncService>(client =>
    client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddSingleton<ICertificateTemplateStorage>(new CertificateTemplateStorage(StoragePath(
    builder.Configuration, "Storage:CertificateTemplatesPath", builder.Environment.ContentRootPath,
    "certificate-templates")));
var portalBaseUrl = builder.Configuration["Portal:BaseUrl"]
    ?? builder.Configuration["WebPortal:Origin"]
    ?? "http://localhost:5099";
var verificationBaseUrl = builder.Configuration["Certificate:VerificationBaseUrl"]
    ?? portalBaseUrl.TrimEnd('/') + "/verify.html";
builder.Services.AddSingleton(new CertificateOptions(verificationBaseUrl));
builder.Services.AddSingleton(new PortalOptions(portalBaseUrl, verificationBaseUrl));
builder.Services.AddSingleton<ICertificatePdfGenerator>(new CertificatePdfGenerator(new CertificateLayout(
    builder.Configuration["Certificate:FontFamily"] ?? "Tahoma",
    builder.Configuration["Certificate:InstitutionArabic"],
    builder.Configuration["Certificate:InstitutionEnglish"])));
builder.Services.AddSingleton<ICertificateFileStorage>(new CertificateDocumentStorage(StoragePath(
    builder.Configuration, "Storage:CertificatesPath", builder.Environment.ContentRootPath, "certificates")));
builder.Services.AddSingleton<ICourseFileStorage>(new CourseFileStorage(StoragePath(
    builder.Configuration, "Storage:CourseFilesPath", builder.Environment.ContentRootPath, "course-files")));
builder.Services.AddSingleton<IQrCodeRenderer, QrCodeRenderer>();
builder.Services.AddSingleton<IEmailSender>(new SmtpEmailSender(new SmtpOptions(
    builder.Configuration["Email:Host"] ?? string.Empty,
    builder.Configuration.GetValue("Email:Port", 587),
    builder.Configuration.GetValue("Email:EnableSsl", true),
    builder.Configuration["Email:UserName"],
    builder.Configuration["Email:Password"],
    builder.Configuration["Email:FromAddress"] ?? string.Empty,
    builder.Configuration["Email:FromName"] ?? "Zarqa University Training Platform")));
builder.Services.AddSingleton<ICredentialCipher>(new AesCredentialCipher(encryptionKey));
builder.Services.AddSingleton<ITokenIssuer>(new JwtTokenIssuer(
    jwtKey,
    TimeSpan.FromHours(builder.Configuration.GetValue<int>("Security:SessionHours")),
    TimeSpan.FromDays(builder.Configuration.GetValue<int>("Security:RememberDays"))));
builder.Services.AddCors(options => options.AddPolicy("portal", policy =>
    policy.WithOrigins(portalOrigin).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = JwtTokenIssuer.Issuer,
            ValidateAudience = true,
            ValidAudience = JwtTokenIssuer.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                context.Token = context.Request.Cookies[JwtTokenIssuer.CookieName];
                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var idText = context.Principal?.FindFirstValue("sub");
                var versionText = context.Principal?.FindFirstValue("auth_version");
                if (!Guid.TryParse(idText, out var id) || !int.TryParse(versionText, out var version))
                {
                    context.Fail("Invalid account token.");
                    return;
                }
                var store = context.HttpContext.RequestServices.GetRequiredService<IAuthStore>();
                var account = await store.FindByIdAsync(id, context.HttpContext.RequestAborted);
                if (account is null || !AuthService.CanSignIn(account) || account.AuthVersion != version)
                    context.Fail("Account token is no longer valid.");
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    foreach (var key in PermissionKeys.All)
        options.AddPolicy(key, policy => policy.RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(key)));
});
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (args.Contains("--migrate", StringComparer.OrdinalIgnoreCase))
{
    // تطبيق ترحيلات قاعدة البيانات ثم الخروج؛ يُستخدم في نشر Docker وسكربتات أوبونتو.
    await using (var scope = app.Services.CreateAsyncScope())
    {
        var database = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
        await database.Database.MigrateAsync();
    }
    Console.WriteLine("Database migrations applied.");
    return;
}

if (args.Contains("--bootstrap-admin", StringComparer.OrdinalIgnoreCase))
{
    if (!app.Environment.IsDevelopment())
        throw new InvalidOperationException("Admin bootstrap is available only in Development.");
    await ZU.Training.WebApi.Bootstrap.DevelopmentAdminBootstrap.RunAsync(app.Services, args);
    return;
}

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

if (!app.Environment.IsDevelopment())
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
                           Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto,
        ForwardLimit = null,
        KnownIPNetworks = { },
        KnownProxies = { }
    });
app.UseCors("portal");
app.UseRateLimiter();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true &&
        context.User.FindFirstValue("password_change_required") == "true" &&
        context.Request.Path.StartsWithSegments("/api") &&
        !context.Request.Path.StartsWithSegments("/api/auth") &&
        !context.Request.Path.StartsWithSegments("/api/health") &&
        !(HttpMethods.IsGet(context.Request.Method) &&
          (context.Request.Path == "/api/settings/hints" || context.Request.Path == "/api/settings/password-policy")))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }
    await next();
});
app.UseAuthorization();
app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/health/database", async (TrainingDbContext db, CancellationToken cancellationToken) =>
    await db.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "ok" })
        : Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable));

app.Run();
