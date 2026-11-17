using System.ComponentModel.DataAnnotations;
using Amanet.Application.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Amanet.Infrastructure.Identity;

public sealed class AdminSeeder(
    UserManager<ApplicationUser> users,
    RoleManager<IdentityRole<int>> roles,
    IOptions<AdminSeedOptions> options)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var seed = options.Value;
        if (string.IsNullOrWhiteSpace(seed.Email) || seed.Email.Length > 256 ||
            !new EmailAddressAttribute().IsValid(seed.Email))
        {
            throw new InvalidOperationException("Configure a valid AdminSeed:Email before seeding.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var user = await users.FindByEmailAsync(seed.Email);
        if (user is null)
        {
            if (string.IsNullOrWhiteSpace(seed.Password))
            {
                throw new InvalidOperationException("Configure AdminSeed:Password before creating the Admin.");
            }

            user = new ApplicationUser { UserName = seed.Email, Email = seed.Email, LockoutEnabled = true };
            EnsureSucceeded(await users.CreateAsync(user, seed.Password));
        }

        // Existing credentials, password hash, lockout state and security stamp are preserved.
        if (!await roles.RoleExistsAsync(AdminAccess.Role))
        {
            EnsureSucceeded(await roles.CreateAsync(new IdentityRole<int>(AdminAccess.Role)));
        }

        if (!await users.IsInRoleAsync(user, AdminAccess.Role))
        {
            EnsureSucceeded(await users.AddToRoleAsync(user, AdminAccess.Role));
        }
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            // Codes identify configuration/Identity errors without echoing secret-bearing input.
            throw new InvalidOperationException("Admin seed failed: " +
                string.Join(", ", result.Errors.Select(error => error.Code)));
        }
    }
}
