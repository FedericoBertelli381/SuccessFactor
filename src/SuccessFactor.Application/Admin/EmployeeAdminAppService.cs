using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SuccessFactor.Employees;
using SuccessFactor.JobRoles;
using SuccessFactor.OrgUnits;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Users;

namespace SuccessFactor.Admin;

[Authorize]
public class EmployeeAdminAppService : ApplicationService, IEmployeeAdminAppService
{
    private readonly ICurrentUser _currentUser;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IRepository<Employee, Guid> _employeeRepository;
    private readonly IRepository<OrgUnit, Guid> _orgUnitRepository;
    private readonly IRepository<JobRole, Guid> _jobRoleRepository;

    public EmployeeAdminAppService(
        ICurrentUser currentUser,
        IAsyncQueryableExecuter asyncExecuter,
        IRepository<Employee, Guid> employeeRepository,
        IRepository<OrgUnit, Guid> orgUnitRepository,
        IRepository<JobRole, Guid> jobRoleRepository)
    {
        _currentUser = currentUser;
        _asyncExecuter = asyncExecuter;
        _employeeRepository = employeeRepository;
        _orgUnitRepository = orgUnitRepository;
        _jobRoleRepository = jobRoleRepository;
    }

    public async Task<EmployeeAdminDto> GetAsync()
    {
        EnsureCurrentUserIsAdmin();

        var employeeQuery = await _employeeRepository.GetQueryableAsync();
        var employees = await _asyncExecuter.ToListAsync(
            employeeQuery
                .OrderBy(x => x.Matricola)
                .ThenBy(x => x.FullName));

        var orgUnitQuery = await _orgUnitRepository.GetQueryableAsync();
        var orgUnits = await _asyncExecuter.ToListAsync(
            orgUnitQuery.OrderBy(x => x.Name));

        var jobRoleQuery = await _jobRoleRepository.GetQueryableAsync();
        var jobRoles = await _asyncExecuter.ToListAsync(
            jobRoleQuery.OrderBy(x => x.Name));

        var orgUnitById = orgUnits.ToDictionary(x => x.Id, x => x.Name);
        var jobRoleById = jobRoles.ToDictionary(x => x.Id, x => x.Name);

        return new EmployeeAdminDto
        {
            Employees = employees.Select(x => new EmployeeAdminListItemDto
            {
                EmployeeId = x.Id,
                UserId = x.UserId,
                Matricola = x.Matricola,
                FullName = x.FullName,
                Email = x.Email,
                OrgUnitId = x.OrgUnitId,
                OrgUnitName = x.OrgUnitId.HasValue && orgUnitById.TryGetValue(x.OrgUnitId.Value, out var orgUnitName) ? orgUnitName : null,
                JobRoleId = x.JobRoleId,
                JobRoleName = x.JobRoleId.HasValue && jobRoleById.TryGetValue(x.JobRoleId.Value, out var jobRoleName) ? jobRoleName : null,
                IsActive = x.IsActive
            }).ToList(),
            OrgUnits = orgUnits.Select(x => new AdminLookupDto
            {
                Id = x.Id,
                Name = x.Name
            }).ToList(),
            JobRoles = jobRoles.Select(x => new AdminLookupDto
            {
                Id = x.Id,
                Name = x.Name
            }).ToList(),
            NewEmployeeDefaults = new CreateUpdateEmployeeDto
            {
                IsActive = true
            }
        };
    }

    public async Task<EmployeeAdminListItemDto> SaveAsync(Guid? id, CreateUpdateEmployeeDto input)
    {
        EnsureCurrentUserIsAdmin();
        input.Matricola = NormalizeRequired(input.Matricola, "Matricola");
        input.FullName = NormalizeRequired(input.FullName, "FullName");
        input.Email = NormalizeNullable(input.Email);
        input.OrgUnitId = NormalizeGuidString(input.OrgUnitId, "OrgUnitIdInvalidFormat");
        input.JobRoleId = NormalizeGuidString(input.JobRoleId, "JobRoleIdInvalidFormat");

        await ValidateReferencesAsync(input);
        await EnsureNoDuplicateMatricolaAsync(id, input.Matricola);

        Employee entity;

        if (id.HasValue)
        {
            entity = await _employeeRepository.GetAsync(id.Value);
        }
        else
        {
            entity = new Employee();
        }

        entity.Matricola = input.Matricola;
        entity.FullName = input.FullName;
        entity.Email = input.Email;
        entity.OrgUnitId = ParseGuidOrNull(input.OrgUnitId);
        entity.JobRoleId = ParseGuidOrNull(input.JobRoleId);
        entity.IsActive = input.IsActive;

        if (id.HasValue)
        {
            entity = await _employeeRepository.UpdateAsync(entity, autoSave: true);
        }
        else
        {
            entity = await _employeeRepository.InsertAsync(entity, autoSave: true);
        }

        var orgUnitName = await ResolveOrgUnitNameAsync(entity.OrgUnitId);
        var jobRoleName = await ResolveJobRoleNameAsync(entity.JobRoleId);

        return new EmployeeAdminListItemDto
        {
            EmployeeId = entity.Id,
            UserId = entity.UserId,
            Matricola = entity.Matricola,
            FullName = entity.FullName,
            Email = entity.Email,
            OrgUnitId = entity.OrgUnitId,
            OrgUnitName = orgUnitName,
            JobRoleId = entity.JobRoleId,
            JobRoleName = jobRoleName,
            IsActive = entity.IsActive
        };
    }

    private void EnsureCurrentUserIsAdmin()
    {
        var roles = _currentUser.Roles ?? Array.Empty<string>();

        if (!roles.Any(x => x.Contains("admin", StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessException("CurrentUserIsNotAdmin");
        }
    }

    private async Task ValidateReferencesAsync(CreateUpdateEmployeeDto input)
    {
        var orgUnitId = ParseGuidOrNull(input.OrgUnitId);
        var jobRoleId = ParseGuidOrNull(input.JobRoleId);

        if (orgUnitId.HasValue && !await _orgUnitRepository.AnyAsync(x => x.Id == orgUnitId.Value))
        {
            throw new BusinessException("OrgUnitNotFound");
        }

        if (jobRoleId.HasValue && !await _jobRoleRepository.AnyAsync(x => x.Id == jobRoleId.Value))
        {
            throw new BusinessException("JobRoleNotFound");
        }
    }

    private async Task EnsureNoDuplicateMatricolaAsync(Guid? excludeId, string matricola)
    {
        if (await _employeeRepository.AnyAsync(x =>
            x.Matricola == matricola &&
            (!excludeId.HasValue || x.Id != excludeId.Value)))
        {
            throw new BusinessException("EmployeeMatricolaAlreadyExists");
        }
    }

    private async Task<string?> ResolveOrgUnitNameAsync(Guid? orgUnitId)
    {
        if (!orgUnitId.HasValue)
        {
            return null;
        }

        var entity = await _orgUnitRepository.FindAsync(orgUnitId.Value);
        return entity?.Name;
    }

    private async Task<string?> ResolveJobRoleNameAsync(Guid? jobRoleId)
    {
        if (!jobRoleId.HasValue)
        {
            return null;
        }

        var entity = await _jobRoleRepository.FindAsync(jobRoleId.Value);
        return entity?.Name;
    }

    private static string NormalizeRequired(string? value, string fieldName)
    {
        var normalized = NormalizeNullable(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new BusinessException($"{fieldName}Required");
        }

        return normalized;
    }

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeGuidString(string? value, string errorCode)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!Guid.TryParse(value, out _))
        {
            throw new BusinessException(errorCode);
        }

        return value.Trim();
    }

    private static Guid? ParseGuidOrNull(string? value)
        => Guid.TryParse(value, out var parsed) ? parsed : null;
}
