using System;
using Microsoft.AspNetCore.Authorization;
using SuccessFactor.Security;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using SuccessFactor.Process;

namespace SuccessFactor.Workflow;

[Authorize(Roles = SuccessFactorRoles.Admin)]

public class PhaseFieldPolicyAppService
    : CrudAppService<PhaseFieldPolicy, PhaseFieldPolicyDto, Guid, PagedAndSortedResultRequestDto, CreateUpdatePhaseFieldPolicyDto>
{
    private readonly IRepository<ProcessTemplate, Guid> _templateRepo;
    private readonly IRepository<ProcessPhase, Guid> _phaseRepo;

    public PhaseFieldPolicyAppService(
        IRepository<PhaseFieldPolicy, Guid> repository,
        IRepository<ProcessTemplate, Guid> templateRepo,
        IRepository<ProcessPhase, Guid> phaseRepo)
        : base(repository)
    {
        _templateRepo = templateRepo;
        _phaseRepo = phaseRepo;
    }

    public override async Task<PhaseFieldPolicyDto> CreateAsync(CreateUpdatePhaseFieldPolicyDto input)
    {
        EnsureTenant();
        ValidateAccess(input.Access);
        await ValidateRefsAsync(input.TemplateId, input.PhaseId);
        return await base.CreateAsync(input);
    }

    public override async Task<PhaseFieldPolicyDto> UpdateAsync(Guid id, CreateUpdatePhaseFieldPolicyDto input)
    {
        EnsureTenant();
        ValidateAccess(input.Access);
        await ValidateRefsAsync(input.TemplateId, input.PhaseId);
        return await base.UpdateAsync(id, input);
    }

    // Endpoint utile: policies effettive (ruolo + fallback "*")
    public async Task<ListResultDto<PhaseFieldPolicyDto>> GetEffectiveForRoleAsync(Guid templateId, Guid phaseId, string roleCode)
    {
        EnsureTenant();
        await ValidateRefsAsync(templateId, phaseId);

        var list = await Repository.GetListAsync(x =>
            x.TemplateId == templateId &&
            x.PhaseId == phaseId &&
            (x.RoleCode == roleCode || x.RoleCode == "*"));

        // se esiste policy specifica per (FieldKey, roleCode), sovrascrive "*"
        var effective = list
            .GroupBy(x => x.FieldKey, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
                g.FirstOrDefault(p => p.RoleCode == roleCode) ??
                g.First(p => p.RoleCode == "*"))
            .ToList();

        return new ListResultDto<PhaseFieldPolicyDto>(effective.Select(ObjectMapper.Map<PhaseFieldPolicy, PhaseFieldPolicyDto>).ToList());
    }

    private void EnsureTenant()
    {
        if (CurrentTenant.Id == null) throw new BusinessException("TenantMissing");
    }

    private static void ValidateAccess(string access)
    {
        if (access is not ("Hidden" or "Read" or "Edit"))
            throw new BusinessException("InvalidFieldAccess")
                .WithData("Allowed", "Hidden|Read|Edit");
    }

    private async Task ValidateRefsAsync(Guid templateId, Guid phaseId)
    {
        if (!await _templateRepo.AnyAsync(t => t.Id == templateId))
            throw new BusinessException("ProcessTemplateNotFound");

        if (!await _phaseRepo.AnyAsync(p => p.Id == phaseId && p.TemplateId == templateId))
            throw new BusinessException("PhaseNotInTemplate");
    }
}