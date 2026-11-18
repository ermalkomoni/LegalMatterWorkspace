using Amanet.Application.Services;
using Amanet.Infrastructure.Data;
using Amanet.Infrastructure.Identity;
using Amanet.Infrastructure.Pdf;
using Amanet.Infrastructure.Services;
using Amanet.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using PdfSharp.Fonts;

namespace Amanet.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services,
        IConfiguration configuration)
    {
        GlobalFontSettings.FontResolver ??= new BundledFontResolver();
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Configure ConnectionStrings:DefaultConnection with user secrets or environment variables.");
        }

        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
            .AddRoles<IdentityRole<int>>()
            .AddSignInManager()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>();
        if (jwt is null || string.IsNullOrWhiteSpace(jwt.Issuer) ||
            string.IsNullOrWhiteSpace(jwt.Audience) || !jwt.HasValidSigningKey())
        {
            throw new InvalidOperationException(
                "Configure Jwt:Issuer, Jwt:Audience and Jwt:SigningKey (base64 of at least 32 random bytes).");
        }

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<AdminSeedOptions>(configuration.GetSection(AdminSeedOptions.SectionName));
        services.AddScoped<AdminSeeder>();
        services.AddScoped<IAdminAuthenticationService, AdminAuthenticationService>();
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<IMatterService, MatterService>();
        services.AddScoped<IMatterActivityService, MatterActivityService>();
        services.AddScoped<IMatterTaskService, MatterTaskService>();
        services.AddScoped<IMatterNoteService, MatterNoteService>();
        services.AddScoped<IMatterDeadlineService, MatterDeadlineService>();
        services.AddSingleton<PrivateDocumentStorage>();
        services.AddScoped<IDocumentService, DocumentService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(bearer =>
            {
                bearer.MapInboundClaims = false;
                bearer.IncludeErrorDetails = false;
                bearer.SaveToken = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(jwt.SigningKey)),
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = "sub",
                    RoleClaimType = "role"
                };
            });
        return services;
    }
}
