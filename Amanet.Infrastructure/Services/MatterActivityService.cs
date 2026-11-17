using Amanet.Application.Services;
using Amanet.Core.Entities;
using Amanet.Core.Enums;
using Amanet.Infrastructure.Data;

namespace Amanet.Infrastructure.Services;

public sealed class MatterActivityService(ApplicationDbContext db) : IMatterActivityService
{
    public void Record(Matter matter, MatterActivityType type, string description)
    {
        db.MatterActivities.Add(new MatterActivity
        {
            Matter = matter,
            Type = type,
            Description = description,
            OccurredAtUtc = DateTimeOffset.UtcNow
        });
    }
}
