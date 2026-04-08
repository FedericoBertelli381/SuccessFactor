using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using SuccessFactor.Competencies; // catalogo competenze (Competency)

namespace SuccessFactor.Competencies.Models;

public class CompetencyModelItemAppService : ApplicationService
{
    private readonly IRepository<CompetencyModel, Guid> _modelRepo;
    private readonly IRepository<CompetencyModelItem, Guid> _itemRepo;
    private readonly IRepository<Competency, Guid> _competencyRepo;

    public CompetencyModelItemAppService(
        IRepository<CompetencyModel, Guid> modelRepo,
        IRepository<CompetencyModelItem, Guid> itemRepo,
        IRepository<Competency, Guid> competencyRepo)
    {
        _modelRepo = modelRepo;
        _itemRepo = itemRepo;
        _competencyRepo = competencyRepo;
    }

    public async Task<CompetencyModelItemDto> AddAsync(AddUpdateCompetencyModelItemDto input)
    {
        EnsureTenant();

        if (!await _modelRepo.AnyAsync(m => m.Id == input.ModelId))
            throw new BusinessException("CompetencyModelNotFound");

        if (!await _competencyRepo.AnyAsync(c => c.Id == input.CompetencyId))
            throw new BusinessException("CompetencyNotFound");

        var dup = await _itemRepo.AnyAsync(x => x.ModelId == input.ModelId && x.CompetencyId == input.CompetencyId);
        if (dup) throw new BusinessException("ModelItemAlreadyExists");

        var entity = ObjectMapper.Map<AddUpdateCompetencyModelItemDto, CompetencyModelItem>(input);
        entity.TenantId = CurrentTenant.Id;

        await _itemRepo.InsertAsync(entity, autoSave: true);
        return ObjectMapper.Map<CompetencyModelItem, CompetencyModelItemDto>(entity);
    }

    public async Task<CompetencyModelItemDto[]> GetByModelAsync(Guid modelId)
    {
        EnsureTenant();
        var list = await _itemRepo.GetListAsync(x => x.ModelId == modelId);
        return list.OrderBy(x => x.CompetencyId).Select(ObjectMapper.Map<CompetencyModelItem, CompetencyModelItemDto>).ToArray();
    }

    public async Task RemoveAsync(Guid modelItemId)
    {
        EnsureTenant();
        await _itemRepo.DeleteAsync(modelItemId, autoSave: true);
    }

    private void EnsureTenant()
    {
        if (CurrentTenant.Id == null) throw new BusinessException("TenantMissing");
    }
}