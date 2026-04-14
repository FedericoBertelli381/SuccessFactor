using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SuccessFactor.Employees;
using SuccessFactor.OrgUnits;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Users;

namespace SuccessFactor.Admin;

[Authorize]
public class AdminOrgUnitAppService : ApplicationService, IAdminOrgUnitAppService
{
    private readonly ICurrentUser _currentUser;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IRepository<OrgUnit, Guid> _orgUnitRepository;
    private readonly IRepository<Employee, Guid> _employeeRepository;

    public AdminOrgUnitAppService(
        ICurrentUser currentUser,
        IAsyncQueryableExecuter asyncExecuter,
        IRepository<OrgUnit, Guid> orgUnitRepository,
        IRepository<Employee, Guid> employeeRepository)
    {
        _currentUser = currentUser;
        _asyncExecuter = asyncExecuter;
        _orgUnitRepository = orgUnitRepository;
        _employeeRepository = employeeRepository;
    }

    public async Task<OrgUnitAdminDto> GetAsync()
    {
        EnsureTenantAndAdmin();

        var orgUnitQuery = await _orgUnitRepository.GetQueryableAsync();
        var orgUnits = await _asyncExecuter.ToListAsync(
            orgUnitQuery
                .OrderBy(x => x.Name));

        var employeeQuery = await _employeeRepository.GetQueryableAsync();
        var employees = await _asyncExecuter.ToListAsync(
            employeeQuery
                .Where(x => x.OrgUnitId.HasValue));

        return new OrgUnitAdminDto
        {
            OrgUnits = MapOrgUnits(orgUnits, employees)
        };
    }

    public async Task<OrgUnitAdminListItemDto> SaveAsync(Guid? orgUnitId, SaveOrgUnitInput input)
    {
        EnsureTenantAndAdmin();
        NormalizeAndValidateInput(input);

        var orgUnitQuery = await _orgUnitRepository.GetQueryableAsync();
        var orgUnits = await _asyncExecuter.ToListAsync(orgUnitQuery);

        ValidateParent(orgUnitId, input.ParentOrgUnitId, orgUnits);
        EnsureNoDuplicateName(orgUnitId, input.Name, orgUnits);

        OrgUnit entity;

        if (orgUnitId.HasValue)
        {
            entity = await _orgUnitRepository.GetAsync(orgUnitId.Value);
        }
        else
        {
            entity = new OrgUnit
            {
                TenantId = CurrentTenant.Id
            };
        }

        entity.Name = input.Name;
        entity.ParentOrgUnitId = input.ParentOrgUnitId;

        entity = orgUnitId.HasValue
            ? await _orgUnitRepository.UpdateAsync(entity, autoSave: true)
            : await _orgUnitRepository.InsertAsync(entity, autoSave: true);

        var refreshedOrgUnits = await _orgUnitRepository.GetListAsync();
        var employees = await _employeeRepository.GetListAsync(x => x.OrgUnitId.HasValue);

        return MapOrgUnits(refreshedOrgUnits, employees)
            .Single(x => x.OrgUnitId == entity.Id);
    }

    public async Task DeleteAsync(Guid orgUnitId)
    {
        EnsureTenantAndAdmin();

        if (orgUnitId == Guid.Empty)
        {
            throw new BusinessException("OrgUnitIdRequired");
        }

        if (await _orgUnitRepository.AnyAsync(x => x.ParentOrgUnitId == orgUnitId))
        {
            throw new BusinessException("OrgUnitHasChildren");
        }

        if (await _employeeRepository.AnyAsync(x => x.OrgUnitId == orgUnitId))
        {
            throw new BusinessException("OrgUnitHasEmployees");
        }

        await _orgUnitRepository.DeleteAsync(orgUnitId);
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

    private static void NormalizeAndValidateInput(SaveOrgUnitInput input)
    {
        input.Name = NormalizeRequired(input.Name, "Name");

        if (input.ParentOrgUnitId == Guid.Empty)
        {
            input.ParentOrgUnitId = null;
        }
    }

    private static void ValidateParent(Guid? orgUnitId, Guid? parentOrgUnitId, List<OrgUnit> orgUnits)
    {
        if (!parentOrgUnitId.HasValue)
        {
            return;
        }

        if (!orgUnits.Any(x => x.Id == parentOrgUnitId.Value))
        {
            throw new BusinessException("ParentOrgUnitNotFound");
        }

        if (orgUnitId.HasValue && parentOrgUnitId.Value == orgUnitId.Value)
        {
            throw new BusinessException("OrgUnitCannotBeParentOfItself");
        }

        if (!orgUnitId.HasValue)
        {
            return;
        }

        var orgUnitById = orgUnits.ToDictionary(x => x.Id, x => x);
        var visited = new HashSet<Guid>();
        var currentParentId = parentOrgUnitId;

        while (currentParentId.HasValue)
        {
            if (!visited.Add(currentParentId.Value))
            {
                throw new BusinessException("OrgUnitHierarchyLoopDetected");
            }

            if (currentParentId.Value == orgUnitId.Value)
            {
                throw new BusinessException("OrgUnitCannotUseDescendantAsParent");
            }

            currentParentId = orgUnitById.TryGetValue(currentParentId.Value, out var currentParent)
                ? currentParent.ParentOrgUnitId
                : null;
        }
    }

    private static void EnsureNoDuplicateName(Guid? orgUnitId, string name, List<OrgUnit> orgUnits)
    {
        if (orgUnits.Any(x =>
            string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase) &&
            (!orgUnitId.HasValue || x.Id != orgUnitId.Value)))
        {
            throw new BusinessException("OrgUnitNameAlreadyExists");
        }
    }

    private static List<OrgUnitAdminListItemDto> MapOrgUnits(List<OrgUnit> orgUnits, List<Employee> employees)
    {
        var orgUnitById = orgUnits.ToDictionary(x => x.Id, x => x);
        var childCountByParentId = orgUnits
            .Where(x => x.ParentOrgUnitId.HasValue)
            .GroupBy(x => x.ParentOrgUnitId!.Value)
            .ToDictionary(x => x.Key, x => x.Count());
        var employeeCountByOrgUnitId = employees
            .Where(x => x.OrgUnitId.HasValue)
            .GroupBy(x => x.OrgUnitId!.Value)
            .ToDictionary(x => x.Key, x => x.Count());

        return orgUnits
            .Select(x =>
            {
                var level = ResolveLevel(x, orgUnitById);
                var childCount = childCountByParentId.GetValueOrDefault(x.Id);
                var employeeCount = employeeCountByOrgUnitId.GetValueOrDefault(x.Id);

                return new OrgUnitAdminListItemDto
                {
                    OrgUnitId = x.Id,
                    Name = x.Name,
                    ParentOrgUnitId = x.ParentOrgUnitId,
                    ParentOrgUnitName = x.ParentOrgUnitId.HasValue && orgUnitById.TryGetValue(x.ParentOrgUnitId.Value, out var parent)
                        ? parent.Name
                        : null,
                    Level = level,
                    ChildCount = childCount,
                    EmployeeCount = employeeCount,
                    CanDelete = childCount == 0 && employeeCount == 0
                };
            })
            .OrderBy(x => BuildSortPath(x.OrgUnitId, orgUnitById))
            .ThenBy(x => x.Name)
            .ToList();
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

    private static string NormalizeRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException($"{fieldName}Required");
        }

        return value.Trim();
    }
}
