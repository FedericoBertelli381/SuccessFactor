using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// TODO: sostituisci questo using con il namespace reale della entity
using SuccessFactor.Workflow;

namespace SuccessFactor.My.Support;

public interface IPhasePermissionResolver
{
    Task<PhaseRolePermission?> GetEffectivePhasePermissionAsync(
        Guid templateId,
        Guid phaseId,
        string roleCode);

    Task<Dictionary<string, string>> GetEffectiveFieldAccessAsync(
        Guid templateId,
        Guid phaseId,
        string roleCode,
        params string[] fieldKeys);
}