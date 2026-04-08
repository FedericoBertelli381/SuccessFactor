using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace SuccessFactor.Competencies.Models;

public class CompetencyModelAppService
    : CrudAppService<CompetencyModel, CompetencyModelDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateCompetencyModelDto>
{
    public CompetencyModelAppService(IRepository<CompetencyModel, Guid> repository) : base(repository) { }

    public override async Task<CompetencyModelDto> CreateAsync(CreateUpdateCompetencyModelDto input)
    {
        EnsureTenant();
        Validate(input);
        return await base.CreateAsync(input);
    }

    public override async Task<CompetencyModelDto> UpdateAsync(Guid id, CreateUpdateCompetencyModelDto input)
    {
        EnsureTenant();
        Validate(input);
        return await base.UpdateAsync(id, input);
    }

    private void EnsureTenant()
    {
        if (CurrentTenant.Id == null) throw new BusinessException("TenantMissing");
    }

    private static void Validate(CreateUpdateCompetencyModelDto input)
    {
        if (input.MinScore > input.MaxScore)
            throw new BusinessException("MinScoreGreaterThanMaxScore");
        if (string.IsNullOrWhiteSpace(input.ScaleType))
            throw new BusinessException("ScaleTypeRequired");
    }
}