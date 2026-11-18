using Amanet.Core.Entities;
using Amanet.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Amanet.Infrastructure.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>(options)
{
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Matter> Matters => Set<Matter>();
    public DbSet<MatterActivity> MatterActivities => Set<MatterActivity>();
    public DbSet<MatterTask> MatterTasks => Set<MatterTask>();
    public DbSet<MatterNote> MatterNotes => Set<MatterNote>();
    public DbSet<MatterDeadline> MatterDeadlines => Set<MatterDeadline>();
    public DbSet<MatterDocument> MatterDocuments => Set<MatterDocument>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<ExternalRequest> ExternalRequests => Set<ExternalRequest>();
    public DbSet<EmailDeliveryAttempt> EmailDeliveryAttempts => Set<EmailDeliveryAttempt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
