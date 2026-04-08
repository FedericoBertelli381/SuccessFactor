using System;
using System.Threading.Tasks;

namespace SuccessFactor.Team.Support;

public interface ITeamWorkflowContextResolver
{
    Task<TeamWorkflowContext> ResolveAsync(Guid targetEmployeeId, Guid? cycleId);
}