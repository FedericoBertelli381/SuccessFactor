using SuccessFactor.JobRoles;
using SuccessFactor.OrgUnits;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace SuccessFactor.Employees;

public class EmployeeAppService : CrudAppService<Employee, EmployeeDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateEmployeeDto>
{
    private readonly IRepository<OrgUnit, Guid> _orgUnitRepo;
    private readonly IRepository<JobRole, Guid> _jobRoleRepo;

    public EmployeeAppService(
        IRepository<Employee, Guid> repository,
        IRepository<OrgUnit, Guid> orgUnitRepo,
        IRepository<JobRole, Guid> jobRoleRepo)
        : base(repository)
    {
        _orgUnitRepo = orgUnitRepo;
        _jobRoleRepo = jobRoleRepo;
    }

    public override async Task<EmployeeDto> CreateAsync(CreateUpdateEmployeeDto input)
    {
        EnsureTenant();
        var orgUnitId = ParseGuidOrNullOrThrow(input.OrgUnitId, "OrgUnitIdInvalidFormat");
        var jobRoleId = ParseGuidOrNullOrThrow(input.JobRoleId, "JobRoleIdInvalidFormat");

        // valida esistenza
        if (orgUnitId.HasValue && !await _orgUnitRepo.AnyAsync(x => x.Id == orgUnitId.Value))
            throw new BusinessException("OrgUnitNotFound");
        if (jobRoleId.HasValue && !await _jobRoleRepo.AnyAsync(x => x.Id == jobRoleId.Value))
            throw new BusinessException("JobRoleNotFound");

        var entity = await MapToEntityAsync(input);
        entity.OrgUnitId = orgUnitId;
        entity.JobRoleId = jobRoleId;
        await ValidateRefsAsync(input);
        return await base.CreateAsync(input);
    }

    public override async Task<EmployeeDto> UpdateAsync(Guid id, CreateUpdateEmployeeDto input)
    {
        EnsureTenant();
        var orgUnitId = ParseGuidOrNullOrThrow(input.OrgUnitId, "OrgUnitIdInvalidFormat");
        var jobRoleId = ParseGuidOrNullOrThrow(input.JobRoleId, "JobRoleIdInvalidFormat");

        // valida esistenza
        if (orgUnitId.HasValue && !await _orgUnitRepo.AnyAsync(x => x.Id == orgUnitId.Value))
            throw new BusinessException("OrgUnitNotFound");
        if (jobRoleId.HasValue && !await _jobRoleRepo.AnyAsync(x => x.Id == jobRoleId.Value))
            throw new BusinessException("JobRoleNotFound");

        var entity = await MapToEntityAsync(input);
        entity.OrgUnitId = orgUnitId;
        entity.JobRoleId = jobRoleId;
        await ValidateRefsAsync(input);
        return await base.UpdateAsync(id, input);
    }

    private void EnsureTenant()
    {
        if (CurrentTenant.Id == null)
            throw new BusinessException("TenantMissing")
                .WithData("Hint", "Aggiungi ?__tenant=NOME_TENANT alla chiamata.");
    }

    private async Task ValidateRefsAsync(CreateUpdateEmployeeDto input)
    {
        var orgUnitId = ParseGuidOrNullOrThrow(input.OrgUnitId, "OrgUnitIdInvalidFormat");
        var jobRoleId = ParseGuidOrNullOrThrow(input.JobRoleId, "JobRoleIdInvalidFormat");

        if (orgUnitId.HasValue && !await _orgUnitRepo.AnyAsync(x => x.Id == orgUnitId.Value))
            throw new BusinessException("OrgUnitNotFound");

        if (jobRoleId.HasValue && !await _jobRoleRepo.AnyAsync(x => x.Id == jobRoleId.Value))
            throw new BusinessException("JobRoleNotFound");
    }
    private static Guid? ParseGuidOrNullOrThrow(string? value, string errorCode)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (!Guid.TryParse(value, out var g))
            throw new BusinessException(errorCode);

        return g;
    }
}