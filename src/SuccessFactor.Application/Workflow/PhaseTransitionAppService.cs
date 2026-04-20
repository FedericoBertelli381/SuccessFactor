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

public class PhaseTransitionAppService
    : CrudAppService<PhaseTransition, PhaseTransitionDto, Guid, PagedAndSortedResultRequestDto, CreateUpdatePhaseTransitionDto>
{
    private readonly IRepository<ProcessTemplate, Guid> _templateRepo;
    private readonly IRepository<ProcessPhase, Guid> _phaseRepo;

    public PhaseTransitionAppService(
        IRepository<PhaseTransition, Guid> repository,
        IRepository<ProcessTemplate, Guid> templateRepo,
        IRepository<ProcessPhase, Guid> phaseRepo)
        : base(repository)
    {
        _templateRepo = templateRepo;
        _phaseRepo = phaseRepo;
    }

    public override async Task<PhaseTransitionDto> CreateAsync(CreateUpdatePhaseTransitionDto input)
    {
        EnsureTenant();
        await EnsureTemplateAndPhasesAsync(input);
        return await base.CreateAsync(input);
    }

    public override async Task<PhaseTransitionDto> UpdateAsync(Guid id, CreateUpdatePhaseTransitionDto input)
    {
        EnsureTenant();
        await EnsureTemplateAndPhasesAsync(input);
        return await base.UpdateAsync(id, input);
    }

    private void EnsureTenant()
    {
        if (CurrentTenant.Id == null) throw new BusinessException("TenantMissing");
    }

    private async Task EnsureTemplateAndPhasesAsync(CreateUpdatePhaseTransitionDto input)
    {
        if (!await _templateRepo.AnyAsync(t => t.Id == input.TemplateId))
            throw new BusinessException("ProcessTemplateNotFound");

        // verifica che le fasi appartengano al template
        if (!await _phaseRepo.AnyAsync(p => p.Id == input.FromPhaseId && p.TemplateId == input.TemplateId))
            throw new BusinessException("FromPhaseNotInTemplate");
        if (!await _phaseRepo.AnyAsync(p => p.Id == input.ToPhaseId && p.TemplateId == input.TemplateId))
            throw new BusinessException("ToPhaseNotInTemplate");
    }
}