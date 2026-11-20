using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Amanet.API.Middleware;
using Amanet.Application;
using Amanet.Application.Services;
using Amanet.Infrastructure;
using Amanet.Infrastructure.Data;
using Amanet.Infrastructure.Identity;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var seedAdmin = args.Contains("--seed-admin", StringComparer.Ordinal);
var builder = WebApplication.CreateBuilder(args.Where(argument => argument != "--seed-admin").ToArray());

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddAuthorization(options =>
{
    var adminPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser()
        .RequireRole(AdminAccess.Role).Build();
    options.DefaultPolicy = adminPolicy;
    options.FallbackPolicy = adminPolicy;
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("AdminLogin", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
    options.AddPolicy("ExternalLinks", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
        }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        await Results.Problem(statusCode: StatusCodes.Status429TooManyRequests,
            title: "Too many requests. Try again later.").ExecuteAsync(context.HttpContext);
    };
});
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
    options.ReportApiVersions = true;
}).AddMvc().AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Amanet API", Version = "v1",
        Description = "Admin access uses eight-hour JWT Bearer tokens. Health is process liveness only. " +
            "Errors use ProblemDetails; validation errors include an errors dictionary and traceId."
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Paste the access token returned by POST /api/v1/auth/login."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});
builder.Services.AddCors(options => options.AddPolicy("LocalFrontend", policy =>
    policy.WithOrigins(new Uri(builder.Configuration["ExternalLinks:FrontendBaseUrl"] ?? "http://localhost:3000")
        .GetLeftPart(UriPartial.Authority)).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Type ??= $"https://httpstatuses.io/{context.ProblemDetails.Status}";
    context.ProblemDetails.Instance = context.HttpContext.Request.Path;
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

var initializeDemo = builder.Configuration.GetValue<bool>("Demo:InitializeDatabase");
// Production migrations are opt-in for the assessment's first deploy.
if (initializeDemo || (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Database:MigrateOnStartup")))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
}

if (seedAdmin || initializeDemo)
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AdminSeeder>().SeedAsync();
}

if (seedAdmin)
    return;

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors("LocalFrontend");
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Amanet API v1"));
}
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
    .AllowAnonymous()
    .WithSummary("Process liveness without PostgreSQL or email readiness checks.");
app.Run();

public partial class Program;
