using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SuccessFactor.Goals;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Users;

namespace SuccessFactor.Admin;

[Authorize]
public class AdminGoalCatalogAppService : ApplicationService, IAdminGoalCatalogAppService
{
    private readonly ICurrentUser _currentUser;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IRepository<Goal, Guid> _goalRepository;
    private readonly IRepository<GoalAssignment, Guid> _goalAssignmentRepository;

    public AdminGoalCatalogAppService(
        ICurrentUser currentUser,
        IAsyncQueryableExecuter asyncExecuter,
        IRepository<Goal, Guid> goalRepository,
        IRepository<GoalAssignment, Guid> goalAssignmentRepository)
    {
        _currentUser = currentUser;
        _asyncExecuter = asyncExecuter;
        _goalRepository = goalRepository;
        _goalAssignmentRepository = goalAssignmentRepository;
    }

    public async Task<GoalCatalogAdminDto> GetAsync()
    {
        EnsureTenantAndAdmin();

        var goalQuery = await _goalRepository.GetQueryableAsync();
        var goals = await _asyncExecuter.ToListAsync(
            goalQuery
                .OrderBy(x => x.Category)
                .ThenBy(x => x.Title));

        var assignmentQuery = await _goalAssignmentRepository.GetQueryableAsync();
        var assignments = await _asyncExecuter.ToListAsync(assignmentQuery);

        return new GoalCatalogAdminDto
        {
            Goals = MapGoals(goals, assignments),
            Categories = goals
                .Select(x => x.Category)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList()
        };
    }

    public async Task<GoalCatalogAdminListItemDto> SaveAsync(Guid? goalId, SaveGoalCatalogItemInput input)
    {
        EnsureTenantAndAdmin();
        NormalizeAndValidate(input);

        var goalQuery = await _goalRepository.GetQueryableAsync();
        var goals = await _asyncExecuter.ToListAsync(goalQuery);
        EnsureNoDuplicateTitle(goalId, input.Title, input.Category, goals);

        Goal entity;

        if (goalId.HasValue)
        {
            entity = await _goalRepository.GetAsync(goalId.Value);
        }
        else
        {
            entity = new Goal
            {
                TenantId = CurrentTenant.Id
            };
        }

        entity.Title = input.Title;
        entity.Description = input.Description;
        entity.Category = input.Category;
        entity.IsLibraryItem = input.IsLibraryItem;
        entity.DefaultWeight = input.DefaultWeight;

        entity = goalId.HasValue
            ? await _goalRepository.UpdateAsync(entity, autoSave: true)
            : await _goalRepository.InsertAsync(entity, autoSave: true);

        var refreshedGoals = await _goalRepository.GetListAsync();
        var assignments = await _goalAssignmentRepository.GetListAsync();

        return MapGoals(refreshedGoals, assignments)
            .Single(x => x.GoalId == entity.Id);
    }

    public async Task DeleteAsync(Guid goalId)
    {
        EnsureTenantAndAdmin();

        if (goalId == Guid.Empty)
        {
            throw new BusinessException("GoalIdRequired");
        }

        if (await _goalAssignmentRepository.AnyAsync(x => x.GoalId == goalId))
        {
            throw new BusinessException("GoalHasAssignments");
        }

        await _goalRepository.DeleteAsync(goalId);
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

    private static void NormalizeAndValidate(SaveGoalCatalogItemInput input)
    {
        input.Title = NormalizeRequired(input.Title, "Title");
        input.Description = NormalizeOptional(input.Description);
        input.Category = NormalizeOptional(input.Category);

        if (input.DefaultWeight is < 0 or > 100)
        {
            throw new BusinessException("GoalDefaultWeightOutOfRange");
        }
    }

    private static void EnsureNoDuplicateTitle(
        Guid? goalId,
        string title,
        string? category,
        List<Goal> goals)
    {
        if (goals.Any(x =>
            string.Equals(x.Title, title, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Category ?? string.Empty, category ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
            (!goalId.HasValue || x.Id != goalId.Value)))
        {
            throw new BusinessException("GoalCatalogItemAlreadyExists");
        }
    }

    private static List<GoalCatalogAdminListItemDto> MapGoals(List<Goal> goals, List<GoalAssignment> assignments)
    {
        var assignmentCountByGoalId = assignments
            .GroupBy(x => x.GoalId)
            .ToDictionary(x => x.Key, x => x.Count());

        return goals
            .Select(x =>
            {
                var assignmentCount = assignmentCountByGoalId.GetValueOrDefault(x.Id);

                return new GoalCatalogAdminListItemDto
                {
                    GoalId = x.Id,
                    Title = x.Title,
                    Description = x.Description,
                    Category = x.Category,
                    IsLibraryItem = x.IsLibraryItem,
                    DefaultWeight = x.DefaultWeight,
                    AssignmentCount = assignmentCount,
                    CanDelete = assignmentCount == 0
                };
            })
            .OrderBy(x => x.Category)
            .ThenBy(x => x.Title)
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

    private static string? NormalizeOptional(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return normalized == "-" ? null : normalized;
    }
}
