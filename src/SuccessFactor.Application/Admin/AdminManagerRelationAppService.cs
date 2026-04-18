using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SuccessFactor.Employees;
using SuccessFactor.Security;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Users;

namespace SuccessFactor.Admin;

[Authorize]
public class AdminManagerRelationAppService : ApplicationService, IAdminManagerRelationAppService
{
    private static readonly string[] AllowedRelationTypes = ["Line", "Functional", "Project", "Hr"];

    private readonly ICurrentUser _currentUser;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IRepository<EmployeeManager, Guid> _relationRepository;
    private readonly IRepository<Employee, Guid> _employeeRepository;

    public AdminManagerRelationAppService(
        ICurrentUser currentUser,
        IAsyncQueryableExecuter asyncExecuter,
        IRepository<EmployeeManager, Guid> relationRepository,
        IRepository<Employee, Guid> employeeRepository)
    {
        _currentUser = currentUser;
        _asyncExecuter = asyncExecuter;
        _relationRepository = relationRepository;
        _employeeRepository = employeeRepository;
    }

    public async Task<ManagerRelationAdminDto> GetAsync()
    {
        EnsureTenantAndAdmin();

        var employeeQuery = await _employeeRepository.GetQueryableAsync();
        var employees = await _asyncExecuter.ToListAsync(
            employeeQuery
                .OrderBy(x => x.Matricola)
                .ThenBy(x => x.FullName));
        var employeeById = employees.ToDictionary(x => x.Id, x => x);

        var relationQuery = await _relationRepository.GetQueryableAsync();
        var relations = await _asyncExecuter.ToListAsync(
            relationQuery
                .OrderBy(x => x.EmployeeId)
                .ThenByDescending(x => x.IsPrimary)
                .ThenBy(x => x.RelationType)
                .ThenBy(x => x.ManagerEmployeeId));

        return new ManagerRelationAdminDto
        {
            Employees = employees
                .Where(x => x.IsActive)
                .Select(MapEmployee)
                .ToList(),
            Relations = relations
                .Select(x => MapRelation(x, employeeById))
                .OrderBy(x => x.EmployeeMatricola)
                .ThenBy(x => x.EmployeeName)
                .ThenByDescending(x => x.IsActive)
                .ThenByDescending(x => x.IsPrimary)
                .ThenBy(x => x.RelationType)
                .ToList()
        };
    }

    public async Task<ManagerRelationAdminListItemDto> SaveAsync(Guid? relationId, SaveManagerRelationInput input)
    {
        EnsureTenantAndAdmin();
        NormalizeAndValidateInput(input);
        await ValidateEmployeesAsync(input);

        EmployeeManager entity;

        if (relationId.HasValue)
        {
            entity = await _relationRepository.GetAsync(relationId.Value);
            await EnsureNoDuplicateAsync(input, entity.Id);
        }
        else
        {
            await EnsureNoDuplicateAsync(input, null);

            entity = new EmployeeManager
            {
                TenantId = CurrentTenant.Id,
                EmployeeId = input.EmployeeId,
                ManagerEmployeeId = input.ManagerEmployeeId
            };
        }

        entity.EmployeeId = input.EmployeeId;
        entity.ManagerEmployeeId = input.ManagerEmployeeId;
        entity.RelationType = input.RelationType;
        entity.IsPrimary = input.IsPrimary;
        entity.StartDate = input.StartDate;
        entity.EndDate = input.EndDate;

        if (entity.IsPrimary)
        {
            await ClearOtherPrimaryRelationsAsync(entity);
        }

        entity = relationId.HasValue
            ? await _relationRepository.UpdateAsync(entity, autoSave: true)
            : await _relationRepository.InsertAsync(entity, autoSave: true);

        var employees = await _employeeRepository.GetListAsync(x =>
            x.Id == entity.EmployeeId || x.Id == entity.ManagerEmployeeId);

        return MapRelation(entity, employees.ToDictionary(x => x.Id, x => x));
    }

    public async Task EndAsync(Guid relationId, DateOnly? endDate = null)
    {
        EnsureTenantAndAdmin();

        var entity = await _relationRepository.GetAsync(relationId);
        var resolvedEndDate = endDate ?? DateOnly.FromDateTime(Clock.Now);

        if (entity.StartDate.HasValue && resolvedEndDate < entity.StartDate.Value)
        {
            throw new BusinessException("EndDateBeforeStartDate");
        }

        entity.EndDate = resolvedEndDate;
        entity.IsPrimary = false;

        await _relationRepository.UpdateAsync(entity, autoSave: true);
    }

    public async Task DeleteAsync(Guid relationId)
    {
        EnsureTenantAndAdmin();
        await _relationRepository.DeleteAsync(relationId);
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

    private async Task ValidateEmployeesAsync(SaveManagerRelationInput input)
    {
        if (!await _employeeRepository.AnyAsync(x => x.Id == input.EmployeeId && x.IsActive))
        {
            throw new BusinessException("EmployeeNotFoundOrInactive");
        }

        if (!await _employeeRepository.AnyAsync(x => x.Id == input.ManagerEmployeeId && x.IsActive))
        {
            throw new BusinessException("ManagerNotFoundOrInactive");
        }
    }

    private async Task EnsureNoDuplicateAsync(SaveManagerRelationInput input, Guid? currentRelationId)
    {
        var duplicate = await _relationRepository.AnyAsync(x =>
            x.EmployeeId == input.EmployeeId &&
            x.ManagerEmployeeId == input.ManagerEmployeeId &&
            x.RelationType == input.RelationType &&
            (!currentRelationId.HasValue || x.Id != currentRelationId.Value));

        if (duplicate)
        {
            throw new BusinessException("ManagerRelationAlreadyExists");
        }
    }

    private async Task ClearOtherPrimaryRelationsAsync(EmployeeManager entity)
    {
        var today = DateOnly.FromDateTime(Clock.Now);
        var relations = await _relationRepository.GetListAsync(x =>
            x.EmployeeId == entity.EmployeeId &&
            x.RelationType == entity.RelationType &&
            x.IsPrimary &&
            x.Id != entity.Id &&
            (!x.StartDate.HasValue || x.StartDate.Value <= today) &&
            (!x.EndDate.HasValue || x.EndDate.Value >= today));

        foreach (var relation in relations)
        {
            relation.IsPrimary = false;
            await _relationRepository.UpdateAsync(relation, autoSave: true);
        }
    }

    private static void NormalizeAndValidateInput(SaveManagerRelationInput input)
    {
        if (input.EmployeeId == Guid.Empty)
        {
            throw new BusinessException("EmployeeIdRequired");
        }

        if (input.ManagerEmployeeId == Guid.Empty)
        {
            throw new BusinessException("ManagerEmployeeIdRequired");
        }

        if (input.EmployeeId == input.ManagerEmployeeId)
        {
            throw new BusinessException("EmployeeCannotManageSelf");
        }

        input.RelationType = string.IsNullOrWhiteSpace(input.RelationType)
            ? "Line"
            : input.RelationType.Trim();

        if (!AllowedRelationTypes.Contains(input.RelationType, StringComparer.OrdinalIgnoreCase))
        {
            throw new BusinessException("ManagerRelationTypeInvalid");
        }

        input.RelationType = AllowedRelationTypes.First(x =>
            string.Equals(x, input.RelationType, StringComparison.OrdinalIgnoreCase));

        if (input.StartDate.HasValue && input.EndDate.HasValue && input.EndDate.Value < input.StartDate.Value)
        {
            throw new BusinessException("EndDateBeforeStartDate");
        }

        if (input.EndDate.HasValue)
        {
            input.IsPrimary = false;
        }
    }

    private static EmployeeAdminListItemDto MapEmployee(Employee employee)
    {
        return new EmployeeAdminListItemDto
        {
            EmployeeId = employee.Id,
            UserId = employee.UserId,
            Matricola = employee.Matricola,
            FullName = employee.FullName,
            Email = employee.Email,
            OrgUnitId = employee.OrgUnitId,
            JobRoleId = employee.JobRoleId,
            IsActive = employee.IsActive
        };
    }

    private static ManagerRelationAdminListItemDto MapRelation(
        EmployeeManager relation,
        Dictionary<Guid, Employee> employeeById)
    {
        employeeById.TryGetValue(relation.EmployeeId, out var employee);
        employeeById.TryGetValue(relation.ManagerEmployeeId, out var manager);

        return new ManagerRelationAdminListItemDto
        {
            RelationId = relation.Id,
            EmployeeId = relation.EmployeeId,
            EmployeeMatricola = employee?.Matricola ?? string.Empty,
            EmployeeName = employee?.FullName ?? string.Empty,
            ManagerEmployeeId = relation.ManagerEmployeeId,
            ManagerMatricola = manager?.Matricola ?? string.Empty,
            ManagerName = manager?.FullName ?? string.Empty,
            RelationType = relation.RelationType,
            IsPrimary = relation.IsPrimary,
            StartDate = relation.StartDate,
            EndDate = relation.EndDate,
            IsActive = IsActive(relation)
        };
    }

    private static bool IsActive(EmployeeManager relation)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        return (!relation.StartDate.HasValue || relation.StartDate.Value <= today) &&
               (!relation.EndDate.HasValue || relation.EndDate.Value >= today);
    }
}
