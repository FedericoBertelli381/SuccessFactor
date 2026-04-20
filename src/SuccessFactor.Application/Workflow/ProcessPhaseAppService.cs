using System;
using Microsoft.AspNetCore.Authorization;
using SuccessFactor.Security;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using SuccessFactor.Process; // ProcessTemplate

namespace SuccessFactor.Workflow;

[Authorize(Roles = SuccessFactorRoles.Admin)]

public class ProcessPhaseAppService
    : CrudAppService<ProcessPhase, ProcessPhaseDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateProcessPhaseDto>
{
    private readonly IRepository<ProcessTemplate, Guid> _templateRepo;

    public ProcessPhaseAppService(
        IRepository<ProcessPhase, Guid> repository,
        IRepository<ProcessTemplate, Guid> templateRepo)
        : base(repository)
    {
        _templateRepo = templateRepo;
    }

    public override async Task<ProcessPhaseDto> CreateAsync(CreateUpdateProcessPhaseDto input)
    {
        EnsureTenant();
        await EnsureTemplateInTenantAsync(input.TemplateId);
        return await base.CreateAsync(input);
    }

    public override async Task<ProcessPhaseDto> UpdateAsync(Guid id, CreateUpdateProcessPhaseDto input)
    {
        EnsureTenant();
        await EnsureTemplateInTenantAsync(input.TemplateId);
        return await base.UpdateAsync(id, input);
    }

    private void EnsureTenant()
    {
        if (CurrentTenant.Id == null) throw new BusinessException("TenantMissing");
    }

    private async Task EnsureTemplateInTenantAsync(Guid templateId)
    {
        var ok = await _templateRepo.AnyAsync(t => t.Id == templateId);
        if (!ok) throw new BusinessException("ProcessTemplateNotFound");
    }
}