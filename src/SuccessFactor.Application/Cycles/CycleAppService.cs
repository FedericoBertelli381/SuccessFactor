using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using SuccessFactor.Process; // <-- se hai rinominato il namespace, cambia qui

namespace SuccessFactor.Cycles;

public class CycleAppService :
    CrudAppService<Cycle, CycleDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateCycleDto>
{
    private readonly IRepository<ProcessTemplate, Guid> _templateRepo;

    public CycleAppService(
        IRepository<Cycle, Guid> repository,
        IRepository<ProcessTemplate, Guid> templateRepo)
        : base(repository)
    {
        _templateRepo = templateRepo;
    }

    public override async Task<CycleDto> CreateAsync(CreateUpdateCycleDto input)
    {
        EnsureTenant();
        await ValidateAsync(input);
        return await base.CreateAsync(input);
    }

    public override async Task<CycleDto> UpdateAsync(Guid id, CreateUpdateCycleDto input)
    {
        EnsureTenant();
        await ValidateAsync(input);
        return await base.UpdateAsync(id, input);
    }

    private void EnsureTenant()
    {
        if (CurrentTenant.Id == null)
            throw new BusinessException("TenantMissing")
                .WithData("Hint", "Aggiungi ?__tenant=NOME_TENANT alla chiamata.");
    }

    private async Task ValidateAsync(CreateUpdateCycleDto input)
    {
        // Date range
        if (input.StartDate.HasValue && input.EndDate.HasValue && input.StartDate.Value > input.EndDate.Value)
            throw new BusinessException("StartDateAfterEndDate");

        // Template must exist (tenant filter di ABP applicato automaticamente)
        var exists = await _templateRepo.AnyAsync(t => t.Id == input.TemplateId);
        if (!exists)
            throw new BusinessException("ProcessTemplateNotFound");
    }
}