using Amanet.Core.Entities;
using Amanet.Core.Enums;

namespace Amanet.Application.Services;

public interface IMatterActivityService
{
    void Record(Matter matter, MatterActivityType type, string description);
}
