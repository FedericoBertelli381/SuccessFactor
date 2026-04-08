using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

// TODO: sostituisci questi using con i namespace reali delle tue entity
using SuccessFactor.Workflow;

namespace SuccessFactor.My.Support;

public class PhasePermissionResolver : IPhasePermissionResolver, ITransientDependency
{
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IRepository<PhaseRolePermission, Guid> _phaseRolePermissionRepository;
    private readonly IRepository<PhaseFieldPolicy, Guid> _phaseFieldPolicyRepository;

    public PhasePermissionResolver(
        IAsyncQueryableExecuter asyncExecuter,
        IRepository<PhaseRolePermission, Guid> phaseRolePermissionRepository,
        IRepository<PhaseFieldPolicy, Guid> phaseFieldPolicyRepository)
    {
        _asyncExecuter = asyncExecuter;
        _phaseRolePermissionRepository = phaseRolePermissionRepository;
        _phaseFieldPolicyRepository = phaseFieldPolicyRepository;
    }

    public async Task<PhaseRolePermission?> GetEffectivePhasePermissionAsync(
        Guid templateId,
        Guid phaseId,
        string roleCode)
    {
        var query = await _phaseRolePermissionRepository.GetQueryableAsync();

        var exact = await _asyncExecuter.FirstOrDefaultAsync(
            query.Where(x =>
                x.TemplateId == templateId &&
                x.PhaseId == phaseId &&
                x.RoleCode == roleCode));

        if (exact is not null)
        {
            return exact;
        }

        return await _asyncExecuter.FirstOrDefaultAsync(
            query.Where(x =>
                x.TemplateId == templateId &&
                x.PhaseId == phaseId &&
                x.RoleCode == "*"));
    }

    public async Task<Dictionary<string, string>> GetEffectiveFieldAccessAsync(
        Guid templateId,
        Guid phaseId,
        string roleCode,
        params string[] fieldKeys)
    {
        var query = await _phaseFieldPolicyRepository.GetQueryableAsync();

        var rows = await _asyncExecuter.ToListAsync(
            query.Where(x =>
                x.TemplateId == templateId &&
                x.PhaseId == phaseId &&
                fieldKeys.Contains(x.FieldKey) &&
                (x.RoleCode == roleCode || x.RoleCode == "*")));

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var fieldKey in fieldKeys)
        {
            var best = rows
                .Where(x => x.FieldKey == fieldKey)
                .OrderByDescending(x => x.RoleCode == roleCode)
                .FirstOrDefault();

            result[fieldKey] = best?.Access ?? "Read";
        }

        return result;
    }
}