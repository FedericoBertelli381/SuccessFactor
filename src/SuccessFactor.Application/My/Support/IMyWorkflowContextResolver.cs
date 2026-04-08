using System;
using System.Threading.Tasks;

namespace SuccessFactor.My.Support;

public interface IMyWorkflowContextResolver
{
    Task<MyWorkflowContext> ResolveAsync(Guid? cycleId);
}