using Amanet.Application.Exceptions;
using Amanet.Core.Entities;

namespace Amanet.Application.Services;

public static class MatterRules
{
    public static void EnsureWritable(Matter matter)
    {
        if (matter.ArchivedAtUtc.HasValue)
        {
            throw new ConflictException("Archived matters cannot be changed.");
        }
    }
}
