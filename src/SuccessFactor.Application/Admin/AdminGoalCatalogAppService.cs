using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SuccessFactor.Auditing;
using SuccessFactor.Goals;
using SuccessFactor.Security;
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
    private readonly IBusinessAuditLogger _auditLogger;

    public AdminGoalCatalogAppService(
        ICurrentUser currentUser,
        IAsyncQueryableExecuter asyncExecuter,
        IRepository<Goal, Guid> goalRepository,
        IRepository<GoalAssignment, Guid> goalAssignmentRepository,
        IBusinessAuditLogger auditLogger)
    {
        _currentUser = currentUser;
        _asyncExecuter = asyncExecuter;
        _goalRepository = goalRepository;
        _goalAssignmentRepository = goalAssignmentRepository;
        _auditLogger = auditLogger;
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

    public async Task<GoalCatalogImportResultDto> ImportAsync(ImportGoalCatalogInput input)
    {
        EnsureTenantAndAdmin();

        if (input is null || string.IsNullOrWhiteSpace(input.Content))
        {
            throw new BusinessException("GoalCatalogImportContentRequired");
        }

        var result = new GoalCatalogImportResultDto();
        var rows = ParseImportRows(input.Content);
        var goals = await _goalRepository.GetListAsync();
        var goalByKey = goals.ToDictionary(x => GoalKey(x.Title, x.Category));
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var validRows = new List<GoalCatalogImportRow>();

        foreach (var row in rows)
        {
            var message = ValidateImportRow(row);
            var key = GoalKey(row.Title, row.Category);

            if (message is null && !seenKeys.Add(key))
            {
                message = "Goal duplicato nel file.";
            }
            else if (message is null && goalByKey.ContainsKey(key) && !input.UpdateExisting)
            {
                message = "Goal gia esistente e aggiornamento disabilitato.";
            }

            AddImportRowResult(result, row.RowNumber, key, message is null ? (goalByKey.ContainsKey(key) ? "Update" : "Create") : "Error", message);

            if (message is null)
            {
                validRows.Add(row);
            }
        }

        result.ErrorCount = result.Rows.Count(x => x.Status == "Error");
        result.HasErrors = result.ErrorCount > 0;

        if (result.HasErrors)
        {
            return result;
        }

        foreach (var row in validRows)
        {
            var key = GoalKey(row.Title, row.Category);
            var isUpdate = goalByKey.TryGetValue(key, out var entity);

            entity ??= new Goal
            {
                TenantId = CurrentTenant.Id
            };

            entity.Title = row.Title;
            entity.Description = row.Description;
            entity.Category = row.Category;
            entity.DefaultWeight = row.DefaultWeight;
            entity.IsLibraryItem = row.IsLibraryItem;

            if (isUpdate)
            {
                await _goalRepository.UpdateAsync(entity, autoSave: false);
                result.UpdatedGoals++;
            }
            else
            {
                await _goalRepository.InsertAsync(entity, autoSave: false);
                goalByKey[key] = entity;
                result.CreatedGoals++;
            }
        }

        await CurrentUnitOfWork!.SaveChangesAsync();
        await _auditLogger.LogAsync("GoalCatalogImportCompleted", nameof(Goal), "bulk", new Dictionary<string, object?>
        {
            ["CreatedGoals"] = result.CreatedGoals,
            ["UpdatedGoals"] = result.UpdatedGoals,
            ["RowsCount"] = result.Rows.Count
        });

        return result;
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

        if (!SuccessFactorRoles.IsAdmin(roles))
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

    private static List<GoalCatalogImportRow> ParseImportRows(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        return content
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n')
            .Select((line, index) => new { Line = line.Trim(), RowNumber = index + 1 })
            .Where(x => !string.IsNullOrWhiteSpace(x.Line))
            .Select(x => new { x.RowNumber, Columns = SplitImportLine(x.Line) })
            .Where(x => !IsHeader(x.Columns, "Title"))
            .Select(x => new GoalCatalogImportRow
            {
                RowNumber = x.RowNumber,
                Title = GetColumn(x.Columns, 0),
                Description = GetColumn(x.Columns, 1),
                Category = GetColumn(x.Columns, 2),
                DefaultWeightText = GetColumn(x.Columns, 3),
                IsLibraryItemText = GetColumn(x.Columns, 4)
            })
            .ToList();
    }

    private static string? ValidateImportRow(GoalCatalogImportRow row)
    {
        try
        {
            row.Title = NormalizeRequired(row.Title, "Title");
            row.Description = NormalizeOptional(row.Description);
            row.Category = NormalizeOptional(row.Category);
            row.DefaultWeight = ParseDecimalOrNull(row.DefaultWeightText);
            row.IsLibraryItem = ParseBoolOrDefaultTrue(row.IsLibraryItemText);

            if (row.DefaultWeight is < 0 or > 100)
            {
                return "Default weight non valido: usa un valore tra 0 e 100.";
            }

            return null;
        }
        catch (BusinessException ex)
        {
            return ex.Code?.Contains("TitleRequired", StringComparison.OrdinalIgnoreCase) == true
                ? "Title obbligatorio."
                : ex.Code ?? ex.Message;
        }
    }

    private static decimal? ParseDecimalOrNull(string? value)
    {
        var normalized = NormalizeOptional(value);
        if (normalized is null)
        {
            return null;
        }

        if (decimal.TryParse(normalized, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var invariantParsed) ||
            decimal.TryParse(normalized, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.CurrentCulture, out invariantParsed))
        {
            return invariantParsed;
        }

        throw new BusinessException("GoalCatalogImportDefaultWeightInvalid");
    }

    private static bool ParseBoolOrDefaultTrue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "true" or "1" or "yes" or "y" or "si" or "s" => true,
            "false" or "0" or "no" or "n" => false,
            _ => throw new BusinessException("GoalCatalogImportIsLibraryInvalid")
        };
    }

    private static string[] SplitImportLine(string line)
    {
        var separator = line.Contains(';') ? ';' : ',';
        return line.Split(separator).Select(x => x.Trim()).ToArray();
    }

    private static bool IsHeader(string[] columns, string firstColumnName)
        => columns.Length > 0 && string.Equals(columns[0], firstColumnName, StringComparison.OrdinalIgnoreCase);

    private static string GetColumn(string[] columns, int index)
        => columns.Length > index ? columns[index].Trim() : string.Empty;

    private static string GoalKey(string title, string? category)
        => $"{title.Trim().ToUpperInvariant()}|{(category ?? string.Empty).Trim().ToUpperInvariant()}";

    private static void AddImportRowResult(GoalCatalogImportResultDto result, int rowNumber, string key, string status, string? message = null)
    {
        result.Rows.Add(new GoalCatalogImportRowResultDto
        {
            RowNumber = rowNumber,
            Key = key,
            Status = status,
            Message = message
        });
    }

    private class GoalCatalogImportRow
    {
        public int RowNumber { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Category { get; set; }
        public string? DefaultWeightText { get; set; }
        public string? IsLibraryItemText { get; set; }
        public decimal? DefaultWeight { get; set; }
        public bool IsLibraryItem { get; set; } = true;
    }
}
