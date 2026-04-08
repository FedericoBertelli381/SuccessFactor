using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace SuccessFactor.Process;

public class ProcessTemplateAppService :
    CrudAppService<ProcessTemplate, ProcessTemplateDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateProcessTemplateDto>
{
    public ProcessTemplateAppService(IRepository<ProcessTemplate, Guid> repository)
        : base(repository)
    {
    }

    public override async Task<ProcessTemplateDto> CreateAsync(CreateUpdateProcessTemplateDto input)
    {
        EnsureTenant();

        // Regola business: un solo default per tenant
        if (input.IsDefault)
            await ClearOtherDefaultsAsync(null);

        return await base.CreateAsync(input);
    }

    public override async Task<ProcessTemplateDto> UpdateAsync(Guid id, CreateUpdateProcessTemplateDto input)
    {
        EnsureTenant();

        if (input.IsDefault)
            await ClearOtherDefaultsAsync(id);

        return await base.UpdateAsync(id, input);
    }

    private void EnsureTenant()
    {
        if (CurrentTenant.Id == null)
            throw new BusinessException("TenantMissing")
                .WithData("Hint", "Aggiungi ?__tenant=NOME_TENANT alla chiamata.");
    }

    private async Task ClearOtherDefaultsAsync(Guid? excludeId)
    {
        var defaults = await Repository.GetListAsync(x => x.IsDefault);

        foreach (var t in defaults)
        {
            if (excludeId.HasValue && t.Id == excludeId.Value)
                continue;

            t.IsDefault = false;
            await Repository.UpdateAsync(t, autoSave: true);
        }
    }
}