using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace SuccessFactor.Competencies;

public class CompetencyAppService :
    CrudAppService<Competency, CompetencyDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateCompetencyDto>
{
    public CompetencyAppService(IRepository<Competency, Guid> repository)
        : base(repository)
    {
    }
}