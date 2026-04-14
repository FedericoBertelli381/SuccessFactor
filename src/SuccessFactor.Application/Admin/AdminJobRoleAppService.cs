using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SuccessFactor.Employees;
using SuccessFactor.JobRoles;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Users;

namespace SuccessFactor.Admin;

[Authorize]
public class AdminJobRoleAppService : ApplicationService, IAdminJobRoleAppService
{
    private readonly ICurrentUser _currentUser;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IRepository<JobRole, Guid> _jobRoleRepository;
    private readonly IRepository<Employee, Guid> _employeeRepository;

    public AdminJobRoleAppService(
        ICurrentUser currentUser,
        IAsyncQueryableExecuter asyncExecuter,
        IRepository<JobRole, Guid> jobRoleRepository,
        IRepository<Employee, Guid> employeeRepository)
    {
        _currentUser = currentUser;
        _asyncExecuter = asyncExecuter;
        _jobRoleRepository = jobRoleRepository;
        _employeeRepository = employeeRepository;
    }

    public async Task<JobRoleAdminDto> GetAsync()
    {
        EnsureTenantAndAdmin();

        var jobRoleQuery = await _jobRoleRepository.GetQueryableAsync();
        var jobRoles = await _asyncExecuter.ToListAsync(
            jobRoleQuery.OrderBy(x => x.Name));

        var employeeQuery = await _employeeRepository.GetQueryableAsync();
        var employees = await _asyncExecuter.ToListAsync(
            employeeQuery.Where(x => x.JobRoleId.HasValue));

        return new JobRoleAdminDto
        {
            JobRoles = MapJobRoles(jobRoles, employees)
        };
    }

    public async Task<JobRoleAdminListItemDto> SaveAsync(Guid? jobRoleId, SaveJobRoleInput input)
    {
        EnsureTenantAndAdmin();
        input.Name = NormalizeRequired(input.Name, "Name");

        var jobRoleQuery = await _jobRoleRepository.GetQueryableAsync();
        var jobRoles = await _asyncExecuter.ToListAsync(jobRoleQuery);
        EnsureNoDuplicateName(jobRoleId, input.Name, jobRoles);

        JobRole entity;

        if (jobRoleId.HasValue)
        {
            entity = await _jobRoleRepository.GetAsync(jobRoleId.Value);
        }
        else
        {
            entity = new JobRole
            {
                TenantId = CurrentTenant.Id
            };
        }

        entity.Name = input.Name;

        entity = jobRoleId.HasValue
            ? await _jobRoleRepository.UpdateAsync(entity, autoSave: true)
            : await _jobRoleRepository.InsertAsync(entity, autoSave: true);

        var refreshedJobRoles = await _jobRoleRepository.GetListAsync();
        var employees = await _employeeRepository.GetListAsync(x => x.JobRoleId.HasValue);

        return MapJobRoles(refreshedJobRoles, employees)
            .Single(x => x.JobRoleId == entity.Id);
    }

    public async Task DeleteAsync(Guid jobRoleId)
    {
        EnsureTenantAndAdmin();

        if (jobRoleId == Guid.Empty)
        {
            throw new BusinessException("JobRoleIdRequired");
        }

        if (await _employeeRepository.AnyAsync(x => x.JobRoleId == jobRoleId))
        {
            throw new BusinessException("JobRoleHasEmployees");
        }

        await _jobRoleRepository.DeleteAsync(jobRoleId);
    }

    private void EnsureTenantAndAdmin()
    {
        if (CurrentTenant.Id is null)
        {
            throw new BusinessException("TenantMissing");
        }

        var roles = _currentUser.Roles ?? Array.Empty<string>();

        if (!roles.Any(x => x.Contains("admin", StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessException("CurrentUserIsNotAdmin");
        }
    }

    private static void EnsureNoDuplicateName(Guid? jobRoleId, string name, List<JobRole> jobRoles)
    {
        if (jobRoles.Any(x =>
            string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase) &&
            (!jobRoleId.HasValue || x.Id != jobRoleId.Value)))
        {
            throw new BusinessException("JobRoleNameAlreadyExists");
        }
    }

    private static List<JobRoleAdminListItemDto> MapJobRoles(List<JobRole> jobRoles, List<Employee> employees)
    {
        var employeeCountByJobRoleId = employees
            .Where(x => x.JobRoleId.HasValue)
            .GroupBy(x => x.JobRoleId!.Value)
            .ToDictionary(x => x.Key, x => x.Count());

        return jobRoles
            .Select(x =>
            {
                var employeeCount = employeeCountByJobRoleId.GetValueOrDefault(x.Id);

                return new JobRoleAdminListItemDto
                {
                    JobRoleId = x.Id,
                    Name = x.Name,
                    EmployeeCount = employeeCount,
                    CanDelete = employeeCount == 0
                };
            })
            .OrderBy(x => x.Name)
            .ToList();
    }

    private static string NormalizeRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException($"{fieldName}Required");
        }

        return value.Trim();
    }
}
