using System;
using Microsoft.AspNetCore.Authorization;
using SuccessFactor.Security;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using SuccessFactor.Process;

namespace SuccessFactor.Workflow;

[Authorize(Roles = SuccessFactorRoles.Admin)]

public class PhaseRolePermissionAppService
    : CrudAppService<PhaseRolePermission, PhaseRolePermissionDto, Guid, PagedAndSortedResultRequestDto, CreateUpdatePhaseRolePermissionDto>
{
    private readonly IRepository<ProcessTemplate, Guid> _templateRepo;
    private readonly IRepository<ProcessPhase, Guid> _phaseRepo;

    public PhaseRolePermissionAppService(
        IRepository<PhaseRolePermission, Guid> repository,
        IRepository<ProcessTemplate, Guid> templateRepo,
        IRepository<ProcessPhase, Guid> phaseRepo)
        : base(repository)
    {
        _templateRepo = templateRepo;
        _phaseRepo = phaseRepo;
    }

    public override async Task<PhaseRolePermissionDto> CreateAsync(CreateUpdatePhaseRolePermissionDto input)
    {
        EnsureTenant();
        await ValidateRefsAsync(input.TemplateId, input.PhaseId);
        return await base.CreateAsync(input);
    }

    public override async Task<PhaseRolePermissionDto> UpdateAsync(Guid id, CreateUpdatePhaseRolePermissionDto input)
    {
        EnsureTenant();
        await ValidateRefsAsync(input.TemplateId, input.PhaseId);
        return await base.UpdateAsync(id, input);
    }

    // Endpoint utile: permesso effettivo per un ruolo (fallback su RoleCode="*")
    public async Task<PhaseRolePermissionDto?> GetEffectiveAsync(Guid templateId, Guid phaseId, string roleCode)
    {
        EnsureTenant();
        await ValidateRefsAsync(templateId, phaseId);

        var exact = await Repository.FirstOrDefaultAsync(x =>
            x.TemplateId == templateId && x.PhaseId == phaseId && x.RoleCode == roleCode);

        if (exact != null) return ObjectMapper.Map<PhaseRolePermission, PhaseRolePermissionDto>(exact);

        var fallback = await Repository.FirstOrDefaultAsync(x =>
            x.TemplateId == templateId && x.PhaseId == phaseId && x.RoleCode == "*");

        return fallback == null ? null : ObjectMapper.Map<PhaseRolePermission, PhaseRolePermissionDto>(fallback);
    }

    private void EnsureTenant()
    {
        if (CurrentTenant.Id == null) throw new BusinessException("TenantMissing");
    }

    private async Task ValidateRefsAsync(Guid templateId, Guid phaseId)
    {
        if (!await _templateRepo.AnyAsync(t => t.Id == templateId))
            throw new BusinessException("ProcessTemplateNotFound");

        if (!await _phaseRepo.AnyAsync(p => p.Id == phaseId && p.TemplateId == templateId))
            throw new BusinessException("PhaseNotInTemplate");
    }
}