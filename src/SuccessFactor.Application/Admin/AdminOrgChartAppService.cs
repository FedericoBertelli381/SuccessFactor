using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SuccessFactor.Employees;
using SuccessFactor.Security;
using SuccessFactor.JobRoles;
using SuccessFactor.OrgUnits;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Users;

namespace SuccessFactor.Admin;

[Authorize]
public class AdminOrgChartAppService : ApplicationService, IAdminOrgChartAppService
{
    private readonly ICurrentUser _currentUser;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IRepository<OrgUnit, Guid> _orgUnitRepository;
    private readonly IRepository<Employee, Guid> _employeeRepository;
    private readonly IRepository<EmployeeManager, Guid> _managerRepository;
    private readonly IRepository<JobRole, Guid> _jobRoleRepository;

    public AdminOrgChartAppService(
        ICurrentUser currentUser,
        IAsyncQueryableExecuter asyncExecuter,
        IRepository<OrgUnit, Guid> orgUnitRepository,
        IRepository<Employee, Guid> employeeRepository,
        IRepository<EmployeeManager, Guid> managerRepository,
        IRepository<JobRole, Guid> jobRoleRepository)
    {
        _currentUser = currentUser;
        _asyncExecuter = asyncExecuter;
        _orgUnitRepository = orgUnitRepository;
        _employeeRepository = employeeRepository;
        _managerRepository = managerRepository;
        _jobRoleRepository = jobRoleRepository;
    }

    public async Task<OrgChartDto> GetAsync()
    {
        EnsureTenantAndAdmin();

        var orgUnitQuery = await _orgUnitRepository.GetQueryableAsync();
        var orgUnits = await _asyncExecuter.ToListAsync(orgUnitQuery.OrderBy(x => x.Name));

        var employeeQuery = await _employeeRepository.GetQueryableAsync();
        var employees = await _asyncExecuter.ToListAsync(
            employeeQuery
                .OrderBy(x => x.Matricola)
                .ThenBy(x => x.FullName));

        var managerQuery = await _managerRepository.GetQueryableAsync();
        var managerRelations = await _asyncExecuter.ToListAsync(
            managerQuery
                .Where(x => x.IsPrimary)
                .OrderBy(x => x.EmployeeId)
                .ThenBy(x => x.RelationType));

        var jobRoleQuery = await _jobRoleRepository.GetQueryableAsync();
        var jobRoles = await _asyncExecuter.ToListAsync(jobRoleQuery.OrderBy(x => x.Name));

        var orgUnitById = orgUnits.ToDictionary(x => x.Id, x => x);
        var employeeById = employees.ToDictionary(x => x.Id, x => x);
        var jobRoleById = jobRoles.ToDictionary(x => x.Id, x => x.Name);
        var activePrimaryManagerByEmployeeId = managerRelations
            .Where(IsActive)
            .GroupBy(x => x.EmployeeId)
            .ToDictionary(x => x.Key, x => x.First());
        var employeesByOrgUnitId = employees
            .Where(x => x.OrgUnitId.HasValue)
            .GroupBy(x => x.OrgUnitId!.Value)
            .ToDictionary(x => x.Key, x => x.ToList());
        var childCountByParentId = orgUnits
            .Where(x => x.ParentOrgUnitId.HasValue)
            .GroupBy(x => x.ParentOrgUnitId!.Value)
            .ToDictionary(x => x.Key, x => x.Count());

        var orgUnitDtos = orgUnits
            .Select(x => MapOrgUnit(
                x,
                orgUnitById,
                employeesByOrgUnitId.GetValueOrDefault(x.Id) ?? [],
                employeeById,
                jobRoleById,
                activePrimaryManagerByEmployeeId,
                childCountByParentId.GetValueOrDefault(x.Id)))
            .OrderBy(x => x.Path)
            .ThenBy(x => x.Name)
            .ToList();

        return new OrgChartDto
        {
            OrgUnits = orgUnitDtos,
            EmployeesWithoutOrgUnit = employees
                .Where(x => !x.OrgUnitId.HasValue)
                .Select(x => MapEmployee(x, null, employeeById, jobRoleById, activePrimaryManagerByEmployeeId))
                .ToList(),
            TotalOrgUnits = orgUnits.Count,
            TotalEmployees = employees.Count,
            EmployeesWithoutOrgUnitCount = employees.Count(x => !x.OrgUnitId.HasValue)
        };
    }

    private void EnsureTenantAndAdmin()
    {
        if (CurrentTenant.Id is null)
        {
            throw new BusinessException("TenantMissing");
        }

        var roles = _currentUser.Roles ?? Array.Empty<string>();

        if (!SuccessFactorRoles.IsAdmin(roles))
        {
            throw new BusinessException("CurrentUserIsNotAdmin");
        }
    }

    private static OrgChartOrgUnitDto MapOrgUnit(
        OrgUnit orgUnit,
        Dictionary<Guid, OrgUnit> orgUnitById,
        List<Employee> directEmployees,
        Dictionary<Guid, Employee> employeeById,
        Dictionary<Guid, string> jobRoleById,
        Dictionary<Guid, EmployeeManager> activePrimaryManagerByEmployeeId,
        int childOrgUnitCount)
    {
        var path = BuildSortPath(orgUnit.Id, orgUnitById);

        return new OrgChartOrgUnitDto
        {
            OrgUnitId = orgUnit.Id,
            Name = orgUnit.Name,
            ParentOrgUnitId = orgUnit.ParentOrgUnitId,
            ParentOrgUnitName = orgUnit.ParentOrgUnitId.HasValue && orgUnitById.TryGetValue(orgUnit.ParentOrgUnitId.Value, out var parent)
                ? parent.Name
                : null,
            Level = ResolveLevel(orgUnit, orgUnitById),
            Path = path,
            DirectEmployeeCount = directEmployees.Count,
            ChildOrgUnitCount = childOrgUnitCount,
            Employees = directEmployees
                .Select(x => MapEmployee(x, orgUnit.Name, employeeById, jobRoleById, activePrimaryManagerByEmployeeId))
                .OrderBy(x => x.Matricola)
                .ThenBy(x => x.FullName)
                .ToList()
        };
    }

    private static OrgChartEmployeeDto MapEmployee(
        Employee employee,
        string? orgUnitName,
        Dictionary<Guid, Employee> employeeById,
        Dictionary<Guid, string> jobRoleById,
        Dictionary<Guid, EmployeeManager> activePrimaryManagerByEmployeeId)
    {
        activePrimaryManagerByEmployeeId.TryGetValue(employee.Id, out var managerRelation);
        Employee? manager = null;

        if (managerRelation is not null)
        {
            employeeById.TryGetValue(managerRelation.ManagerEmployeeId, out manager);
        }

        return new OrgChartEmployeeDto
        {
            EmployeeId = employee.Id,
            Matricola = employee.Matricola,
            FullName = employee.FullName,
            Email = employee.Email,
            OrgUnitId = employee.OrgUnitId,
            OrgUnitName = orgUnitName,
            JobRoleId = employee.JobRoleId,
            JobRoleName = employee.JobRoleId.HasValue && jobRoleById.TryGetValue(employee.JobRoleId.Value, out var jobRoleName)
                ? jobRoleName
                : null,
            PrimaryManagerEmployeeId = manager?.Id,
            PrimaryManagerName = manager?.FullName,
            PrimaryManagerMatricola = manager?.Matricola,
            IsActive = employee.IsActive
        };
    }

    private static int ResolveLevel(OrgUnit orgUnit, Dictionary<Guid, OrgUnit> orgUnitById)
    {
        var level = 0;
        var visited = new HashSet<Guid> { orgUnit.Id };
        var currentParentId = orgUnit.ParentOrgUnitId;

        while (currentParentId.HasValue &&
               visited.Add(currentParentId.Value) &&
               orgUnitById.TryGetValue(currentParentId.Value, out var parent))
        {
            level++;
            currentParentId = parent.ParentOrgUnitId;
        }

        return level;
    }

    private static string BuildSortPath(Guid orgUnitId, Dictionary<Guid, OrgUnit> orgUnitById)
    {
        var parts = new Stack<string>();
        var visited = new HashSet<Guid>();
        var currentId = orgUnitId;

        while (visited.Add(currentId) && orgUnitById.TryGetValue(currentId, out var current))
        {
            parts.Push(current.Name);

            if (!current.ParentOrgUnitId.HasValue)
            {
                break;
            }

            currentId = current.ParentOrgUnitId.Value;
        }

        return string.Join(" / ", parts);
    }

    private static bool IsActive(EmployeeManager relation)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        return (!relation.StartDate.HasValue || relation.StartDate.Value <= today) &&
               (!relation.EndDate.HasValue || relation.EndDate.Value >= today);
    }
}
