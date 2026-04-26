using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SuccessFactor.Auditing;
using SuccessFactor.Cycles;
using SuccessFactor.Security;
using SuccessFactor.Employees;
using SuccessFactor.Goals;
using SuccessFactor.Process;
using SuccessFactor.Workflow;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Users;

namespace SuccessFactor.Admin;

[Authorize]
public class AdminGoalAssignmentAppService : ApplicationService, IAdminGoalAssignmentAppService
{
    private static readonly string[] AllowedAssignmentStatuses = ["Draft", "Approved", "InProgress", "Closed"];

    private readonly ICurrentUser _currentUser;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IRepository<Cycle, Guid> _cycleRepository;
    private readonly IRepository<CycleParticipant, Guid> _participantRepository;
    private readonly IRepository<Employee, Guid> _employeeRepository;
    private readonly IRepository<Goal, Guid> _goalRepository;
    private readonly IRepository<GoalAssignment, Guid> _assignmentRepository;
    private readonly IRepository<ProcessTemplate, Guid> _templateRepository;
    private readonly IRepository<ProcessPhase, Guid> _phaseRepository;
    private readonly IBusinessAuditLogger _auditLogger;

    public AdminGoalAssignmentAppService(
        ICurrentUser currentUser,
        IAsyncQueryableExecuter asyncExecuter,
        IRepository<Cycle, Guid> cycleRepository,
        IRepository<CycleParticipant, Guid> participantRepository,
        IRepository<Employee, Guid> employeeRepository,
        IRepository<Goal, Guid> goalRepository,
        IRepository<GoalAssignment, Guid> assignmentRepository,
        IRepository<ProcessTemplate, Guid> templateRepository,
        IRepository<ProcessPhase, Guid> phaseRepository,
        IBusinessAuditLogger auditLogger)
    {
        _currentUser = currentUser;
        _asyncExecuter = asyncExecuter;
        _cycleRepository = cycleRepository;
        _participantRepository = participantRepository;
        _employeeRepository = employeeRepository;
        _goalRepository = goalRepository;
        _assignmentRepository = assignmentRepository;
        _templateRepository = templateRepository;
        _phaseRepository = phaseRepository;
        _auditLogger = auditLogger;
    }

    public async Task<GoalAssignmentAdminDto> GetAsync(Guid? cycleId = null)
    {
        EnsureTenantAndAdmin();

        var cycleQuery = await _cycleRepository.GetQueryableAsync();
        var cycles = await _asyncExecuter.ToListAsync(
            cycleQuery
                .OrderByDescending(x => x.CycleYear)
                .ThenBy(x => x.Name));

        var selectedCycle = ResolveSelectedCycle(cycles, cycleId);

        var templateIds = cycles.Select(x => x.TemplateId).Distinct().ToList();
        var templateQuery = await _templateRepository.GetQueryableAsync();
        var templates = await _asyncExecuter.ToListAsync(templateQuery.Where(x => templateIds.Contains(x.Id)));
        var templateById = templates.ToDictionary(x => x.Id, x => x);

        var phases = new List<ProcessPhase>();
        if (selectedCycle is not null)
        {
            var phaseQuery = await _phaseRepository.GetQueryableAsync();
            phases = await _asyncExecuter.ToListAsync(
                phaseQuery.Where(x => x.TemplateId == selectedCycle.TemplateId));
        }
        var phaseById = phases.ToDictionary(x => x.Id, x => x);

        var goalQuery = await _goalRepository.GetQueryableAsync();
        var goals = await _asyncExecuter.ToListAsync(
            goalQuery
                .OrderByDescending(x => x.IsLibraryItem)
                .ThenBy(x => x.Category)
                .ThenBy(x => x.Title));
        var goalById = goals.ToDictionary(x => x.Id, x => x);

        var participants = new List<CycleParticipant>();
        var assignments = new List<GoalAssignment>();
        if (selectedCycle is not null)
        {
            var participantQuery = await _participantRepository.GetQueryableAsync();
            participants = await _asyncExecuter.ToListAsync(
                participantQuery
                    .Where(x => x.CycleId == selectedCycle.Id)
                    .OrderBy(x => x.EmployeeId));

            var assignmentQuery = await _assignmentRepository.GetQueryableAsync();
            assignments = await _asyncExecuter.ToListAsync(
                assignmentQuery
                    .Where(x => x.CycleId == selectedCycle.Id)
                    .OrderBy(x => x.EmployeeId));
        }

        var employeeIds = participants.Select(x => x.EmployeeId)
            .Concat(assignments.Select(x => x.EmployeeId))
            .Distinct()
            .ToList();
        var employeeQuery = await _employeeRepository.GetQueryableAsync();
        var employees = await _asyncExecuter.ToListAsync(employeeQuery.Where(x => employeeIds.Contains(x.Id)));
        var employeeById = employees.ToDictionary(x => x.Id, x => x);

        var assignmentCountsByGoalId = assignments
            .GroupBy(x => x.GoalId)
            .ToDictionary(x => x.Key, x => x.Count());

        return new GoalAssignmentAdminDto
        {
            SelectedCycleId = selectedCycle?.Id,
            SelectedCycleName = selectedCycle?.Name,
            SelectedCycleStatus = selectedCycle?.Status,
            CanEditSelectedCycle = selectedCycle is not null && !IsClosed(selectedCycle),
            Cycles = cycles.Select(x => MapCycle(x, templateById)).ToList(),
            Participants = participants
                .Select(x => MapParticipant(x, employeeById, phaseById))
                .OrderBy(x => x.Matricola)
                .ThenBy(x => x.EmployeeName)
                .ToList(),
            Goals = goals
                .Select(x => MapGoal(x, assignmentCountsByGoalId.GetValueOrDefault(x.Id)))
                .ToList(),
            Assignments = assignments
                .Select(x => MapAssignment(x, employeeById, goalById))
                .OrderBy(x => x.Matricola)
                .ThenBy(x => x.EmployeeName)
                .ThenBy(x => x.GoalTitle)
                .ToList()
        };
    }

    public async Task<GoalAssignmentAdminListItemDto> SaveAsync(Guid? assignmentId, SaveGoalAssignmentAdminInput input)
    {
        EnsureTenantAndAdmin();
        NormalizeAndValidate(input);

        var cycle = await _cycleRepository.GetAsync(input.CycleId);
        EnsureCycleEditable(cycle);
        await ValidateReferencesAsync(input);
        await ValidateBusinessRulesAsync(assignmentId, input);

        GoalAssignment entity;
        if (assignmentId.HasValue)
        {
            entity = await _assignmentRepository.GetAsync(assignmentId.Value);

            if (entity.CycleId != input.CycleId)
            {
                throw new BusinessException("GoalAssignmentCycleMismatch");
            }
        }
        else
        {
            entity = new GoalAssignment
            {
                TenantId = CurrentTenant.Id,
                CycleId = input.CycleId
            };
        }

        entity.EmployeeId = input.EmployeeId;
        entity.GoalId = input.GoalId;
        entity.Weight = input.Weight;
        entity.TargetValue = input.TargetValue;
        entity.StartDate = input.StartDate;
        entity.DueDate = input.DueDate;
        entity.Status = input.Status;

        entity = assignmentId.HasValue
            ? await _assignmentRepository.UpdateAsync(entity, autoSave: true)
            : await _assignmentRepository.InsertAsync(entity, autoSave: true);

        var employee = await _employeeRepository.GetAsync(entity.EmployeeId);
        var goal = await _goalRepository.GetAsync(entity.GoalId);

        return MapAssignment(
            entity,
            new Dictionary<Guid, Employee> { [employee.Id] = employee },
            new Dictionary<Guid, Goal> { [goal.Id] = goal });
    }

    public async Task<GoalAssignmentImportResultDto> ImportAsync(ImportGoalAssignmentsInput input)
    {
        EnsureTenantAndAdmin();

        if (input is null || input.CycleId == Guid.Empty)
        {
            throw new BusinessException("CycleIdRequired");
        }

        if (string.IsNullOrWhiteSpace(input.Content))
        {
            throw new BusinessException("GoalAssignmentImportContentRequired");
        }

        var cycle = await _cycleRepository.GetAsync(input.CycleId);
        EnsureCycleEditable(cycle);

        var participants = await _participantRepository.GetListAsync(x =>
            x.CycleId == input.CycleId &&
            !string.Equals(x.Status, "Excluded", StringComparison.OrdinalIgnoreCase));
        var participantByEmployeeId = participants.ToDictionary(x => x.EmployeeId);

        var employeeIds = participants.Select(x => x.EmployeeId).ToList();
        var employees = await _employeeRepository.GetListAsync(x => employeeIds.Contains(x.Id));
        var employeeByMatricola = employees.ToDictionary(x => x.Matricola, StringComparer.OrdinalIgnoreCase);
        var employeeById = employees.ToDictionary(x => x.Id);

        var goals = await _goalRepository.GetListAsync();
        var goalById = goals.ToDictionary(x => x.Id);
        var goalByTitle = goals
            .GroupBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() == 1)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var existingAssignments = await _assignmentRepository.GetListAsync(x => x.CycleId == input.CycleId);
        var assignmentByKey = existingAssignments.ToDictionary(x => AssignmentKey(x.EmployeeId, x.GoalId));

        var result = new GoalAssignmentImportResultDto();
        var rows = ParseImportRows(input.Content);
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var validRows = new List<GoalAssignmentImportRow>();

        foreach (var row in rows)
        {
            var message = ResolveImportRow(row, employeeByMatricola, participantByEmployeeId, goalById, goalByTitle);
            if (message is null)
            {
                row.AssignmentKey = AssignmentKey(row.EmployeeId!.Value, row.GoalId!.Value);

                if (!seenKeys.Add(row.AssignmentKey))
                {
                    message = "Assignment duplicato nel file.";
                }
                else if (!input.UpdateExisting && assignmentByKey.ContainsKey(row.AssignmentKey))
                {
                    message = "Assignment gia esistente e aggiornamento disabilitato.";
                }
            }

            AddImportRowResult(
                result,
                row.RowNumber,
                $"{row.EmployeeMatricola}|{row.GoalLookup}",
                message is null ? (assignmentByKey.ContainsKey(row.AssignmentKey ?? string.Empty) ? "Update" : "Create") : "Error",
                message);

            if (message is null)
            {
                validRows.Add(row);
            }
        }

        if (!result.Rows.Any(x => x.Status == "Error"))
        {
            ValidateProjectedWeights(validRows, existingAssignments, result);
        }

        result.ErrorCount = result.Rows.Count(x => x.Status == "Error");
        result.HasErrors = result.ErrorCount > 0;
        if (result.HasErrors)
        {
            return result;
        }

        foreach (var row in validRows)
        {
            var isUpdate = assignmentByKey.TryGetValue(row.AssignmentKey!, out var entity);
            entity ??= new GoalAssignment
            {
                TenantId = CurrentTenant.Id,
                CycleId = input.CycleId,
                EmployeeId = row.EmployeeId!.Value,
                GoalId = row.GoalId!.Value
            };

            entity.Weight = row.Weight!.Value;
            entity.TargetValue = row.TargetValue;
            entity.StartDate = row.StartDate;
            entity.DueDate = row.DueDate;
            entity.Status = row.Status;

            if (isUpdate)
            {
                await _assignmentRepository.UpdateAsync(entity, autoSave: false);
                result.UpdatedAssignments++;
            }
            else
            {
                await _assignmentRepository.InsertAsync(entity, autoSave: false);
                assignmentByKey[row.AssignmentKey!] = entity;
                result.CreatedAssignments++;
            }
        }

        await CurrentUnitOfWork!.SaveChangesAsync();
        await _auditLogger.LogAsync("GoalAssignmentImportCompleted", nameof(GoalAssignment), cycle.Id.ToString(), new Dictionary<string, object?>
        {
            ["CycleName"] = cycle.Name,
            ["CreatedAssignments"] = result.CreatedAssignments,
            ["UpdatedAssignments"] = result.UpdatedAssignments,
            ["RowsCount"] = result.Rows.Count
        });

        return result;
    }

    public async Task DeleteAsync(Guid assignmentId)
    {
        EnsureTenantAndAdmin();

        var assignment = await _assignmentRepository.GetAsync(assignmentId);
        var cycle = await _cycleRepository.GetAsync(assignment.CycleId);
        EnsureCycleEditable(cycle);

        await _assignmentRepository.DeleteAsync(assignment, autoSave: true);
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

    private async Task ValidateReferencesAsync(SaveGoalAssignmentAdminInput input)
    {
        if (!await _participantRepository.AnyAsync(x =>
                x.CycleId == input.CycleId &&
                x.EmployeeId == input.EmployeeId &&
                x.Status != "Excluded"))
        {
            throw new BusinessException("EmployeeIsNotActiveCycleParticipant");
        }

        if (!await _goalRepository.AnyAsync(x => x.Id == input.GoalId))
        {
            throw new BusinessException("GoalNotFound");
        }
    }

    private async Task ValidateBusinessRulesAsync(Guid? assignmentId, SaveGoalAssignmentAdminInput input)
    {
        if (input.StartDate.HasValue && input.DueDate.HasValue && input.StartDate.Value > input.DueDate.Value)
        {
            throw new BusinessException("StartDateAfterDueDate");
        }

        if (await _assignmentRepository.AnyAsync(x =>
                x.CycleId == input.CycleId &&
                x.EmployeeId == input.EmployeeId &&
                x.GoalId == input.GoalId &&
                (!assignmentId.HasValue || x.Id != assignmentId.Value)))
        {
            throw new BusinessException("GoalAlreadyAssignedToEmployee");
        }

        var assignments = await _assignmentRepository.GetListAsync(x =>
            x.CycleId == input.CycleId &&
            x.EmployeeId == input.EmployeeId);

        var sumOther = assignments
            .Where(x => !assignmentId.HasValue || x.Id != assignmentId.Value)
            .Sum(x => x.Weight);

        if (sumOther + input.Weight > 100m)
        {
            throw new BusinessException("TotalWeightExceeds100")
                .WithData("Current", sumOther)
                .WithData("NewWeight", input.Weight);
        }
    }

    private static Cycle? ResolveSelectedCycle(List<Cycle> cycles, Guid? cycleId)
    {
        if (cycles.Count == 0)
        {
            return null;
        }

        if (cycleId.HasValue)
        {
            var selected = cycles.FirstOrDefault(x => x.Id == cycleId.Value);

            if (selected is null)
            {
                throw new BusinessException("CycleNotFound");
            }

            return selected;
        }

        return cycles
            .OrderByDescending(x => string.Equals(x.Status, "Active", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(x => x.CycleYear)
            .ThenBy(x => x.Name)
            .First();
    }

    private static void NormalizeAndValidate(SaveGoalAssignmentAdminInput input)
    {
        if (input.CycleId == Guid.Empty)
        {
            throw new BusinessException("CycleIdRequired");
        }

        if (input.EmployeeId == Guid.Empty)
        {
            throw new BusinessException("EmployeeIdRequired");
        }

        if (input.GoalId == Guid.Empty)
        {
            throw new BusinessException("GoalIdRequired");
        }

        if (input.Weight is < 0 or > 100)
        {
            throw new BusinessException("GoalAssignmentWeightOutOfRange");
        }

        input.Status = string.IsNullOrWhiteSpace(input.Status) ? "Draft" : input.Status.Trim();

        if (!AllowedAssignmentStatuses.Contains(input.Status, StringComparer.OrdinalIgnoreCase))
        {
            throw new BusinessException("GoalAssignmentStatusInvalid");
        }

        input.Status = AllowedAssignmentStatuses.First(x => string.Equals(x, input.Status, StringComparison.OrdinalIgnoreCase));
    }

    private static void EnsureCycleEditable(Cycle cycle)
    {
        if (IsClosed(cycle))
        {
            throw new BusinessException("ClosedCycleGoalAssignmentsCannotBeChanged");
        }
    }

    private static bool IsClosed(Cycle cycle)
        => string.Equals(cycle.Status, "Closed", StringComparison.OrdinalIgnoreCase);

    private static CycleAdminListItemDto MapCycle(Cycle cycle, Dictionary<Guid, ProcessTemplate> templateById)
    {
        templateById.TryGetValue(cycle.TemplateId, out var template);

        return new CycleAdminListItemDto
        {
            CycleId = cycle.Id,
            Name = cycle.Name,
            CycleYear = cycle.CycleYear,
            TemplateId = cycle.TemplateId,
            TemplateName = template?.Name ?? string.Empty,
            CurrentPhaseId = cycle.CurrentPhaseId,
            Status = cycle.Status,
            StartDate = cycle.StartDate,
            EndDate = cycle.EndDate
        };
    }

    private static CycleParticipantAdminListItemDto MapParticipant(
        CycleParticipant participant,
        Dictionary<Guid, Employee> employeeById,
        Dictionary<Guid, ProcessPhase> phaseById)
    {
        employeeById.TryGetValue(participant.EmployeeId, out var employee);

        ProcessPhase? phase = null;
        if (participant.CurrentPhaseId.HasValue)
        {
            phaseById.TryGetValue(participant.CurrentPhaseId.Value, out phase);
        }

        return new CycleParticipantAdminListItemDto
        {
            ParticipantId = participant.Id,
            CycleId = participant.CycleId,
            EmployeeId = participant.EmployeeId,
            Matricola = employee?.Matricola ?? string.Empty,
            EmployeeName = employee?.FullName ?? string.Empty,
            EmployeeEmail = employee?.Email,
            CurrentPhaseId = participant.CurrentPhaseId,
            CurrentPhaseCode = phase?.Code,
            CurrentPhaseName = phase?.Name,
            Status = participant.Status
        };
    }

    private static GoalCatalogAdminListItemDto MapGoal(Goal goal, int assignmentCount)
    {
        return new GoalCatalogAdminListItemDto
        {
            GoalId = goal.Id,
            Title = goal.Title,
            Description = goal.Description,
            Category = goal.Category,
            IsLibraryItem = goal.IsLibraryItem,
            DefaultWeight = goal.DefaultWeight,
            AssignmentCount = assignmentCount,
            CanDelete = assignmentCount == 0
        };
    }

    private static GoalAssignmentAdminListItemDto MapAssignment(
        GoalAssignment assignment,
        Dictionary<Guid, Employee> employeeById,
        Dictionary<Guid, Goal> goalById)
    {
        employeeById.TryGetValue(assignment.EmployeeId, out var employee);
        goalById.TryGetValue(assignment.GoalId, out var goal);

        return new GoalAssignmentAdminListItemDto
        {
            AssignmentId = assignment.Id,
            CycleId = assignment.CycleId,
            EmployeeId = assignment.EmployeeId,
            Matricola = employee?.Matricola ?? string.Empty,
            EmployeeName = employee?.FullName ?? string.Empty,
            GoalId = assignment.GoalId,
            GoalTitle = goal?.Title ?? string.Empty,
            GoalCategory = goal?.Category,
            Weight = assignment.Weight,
            TargetValue = assignment.TargetValue,
            StartDate = assignment.StartDate,
            DueDate = assignment.DueDate,
            Status = assignment.Status
        };
    }

    private static List<GoalAssignmentImportRow> ParseImportRows(string? content)
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
            .Where(x => !IsHeader(x.Columns, "EmployeeMatricola"))
            .Select(x => new GoalAssignmentImportRow
            {
                RowNumber = x.RowNumber,
                EmployeeMatricola = GetColumn(x.Columns, 0),
                GoalLookup = GetColumn(x.Columns, 1),
                WeightText = GetColumn(x.Columns, 2),
                TargetValueText = GetColumn(x.Columns, 3),
                StartDateText = GetColumn(x.Columns, 4),
                DueDateText = GetColumn(x.Columns, 5),
                Status = GetColumn(x.Columns, 6)
            })
            .ToList();
    }

    private static string? ResolveImportRow(
        GoalAssignmentImportRow row,
        Dictionary<string, Employee> employeeByMatricola,
        Dictionary<Guid, CycleParticipant> participantByEmployeeId,
        Dictionary<Guid, Goal> goalById,
        Dictionary<string, Goal> goalByTitle)
    {
        row.EmployeeMatricola = NormalizeImportRequired(row.EmployeeMatricola);
        row.GoalLookup = NormalizeImportRequired(row.GoalLookup);
        row.Status = string.IsNullOrWhiteSpace(row.Status) ? "Draft" : row.Status.Trim();

        if (!employeeByMatricola.TryGetValue(row.EmployeeMatricola, out var employee))
        {
            return "EmployeeMatricola non trovata tra i participant attivi del ciclo.";
        }

        if (!participantByEmployeeId.ContainsKey(employee.Id))
        {
            return "Employee non presente come participant attivo del ciclo.";
        }

        row.EmployeeId = employee.Id;

        Goal? goal = null;
        if (Guid.TryParse(row.GoalLookup, out var goalId))
        {
            goalById.TryGetValue(goalId, out goal);
        }
        else
        {
            goalByTitle.TryGetValue(row.GoalLookup, out goal);
        }

        if (goal is null)
        {
            return "Goal non trovato o non univoco.";
        }

        row.GoalId = goal.Id;

        try
        {
            row.Weight = ParseDecimalOrNull(row.WeightText) ?? goal.DefaultWeight;
            row.TargetValue = ParseDecimalOrNull(row.TargetValueText);
            row.StartDate = ParseDateOrNull(row.StartDateText);
            row.DueDate = ParseDateOrNull(row.DueDateText);
        }
        catch (BusinessException ex)
        {
            return ex.Code switch
            {
                "ImportDecimalInvalidFormat" => "Valore numerico non valido.",
                "ImportDateInvalidFormat" => "Formato data non valido.",
                _ => ex.Code ?? ex.Message
            };
        }

        if (!row.Weight.HasValue)
        {
            return "Weight obbligatorio: se la colonna e vuota serve un DefaultWeight sul goal.";
        }

        if (row.Weight is < 0 or > 100)
        {
            return "Weight non valido: usa un valore tra 0 e 100.";
        }

        if (row.StartDate.HasValue && row.DueDate.HasValue && row.StartDate.Value > row.DueDate.Value)
        {
            return "Start date non puo essere successiva alla due date.";
        }

        if (!AllowedAssignmentStatuses.Contains(row.Status, StringComparer.OrdinalIgnoreCase))
        {
            return "Status assignment non valido.";
        }

        row.Status = AllowedAssignmentStatuses.First(x => string.Equals(x, row.Status, StringComparison.OrdinalIgnoreCase));
        return null;
    }

    private static void ValidateProjectedWeights(
        List<GoalAssignmentImportRow> rows,
        List<GoalAssignment> existingAssignments,
        GoalAssignmentImportResultDto result)
    {
        var existingByEmployee = existingAssignments
            .GroupBy(x => x.EmployeeId)
            .ToDictionary(x => x.Key, x => x.ToList());

        foreach (var employeeGroup in rows.GroupBy(x => x.EmployeeId!.Value))
        {
            var total = existingByEmployee.TryGetValue(employeeGroup.Key, out var current)
                ? current.Sum(x => x.Weight)
                : 0m;

            foreach (var row in employeeGroup)
            {
                if (current is not null)
                {
                    var currentAssignment = current.FirstOrDefault(x => x.GoalId == row.GoalId!.Value);
                    if (currentAssignment is not null)
                    {
                        total -= currentAssignment.Weight;
                    }
                }

                total += row.Weight!.Value;
            }

            if (total > 100m)
            {
                foreach (var row in employeeGroup)
                {
                    AddImportRowResult(result, row.RowNumber, $"{row.EmployeeMatricola}|{row.GoalLookup}", "Error", "La somma dei pesi goal per questo employee/ciclo supera 100.");
                }
            }
        }
    }

    private static string NormalizeImportRequired(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException("ImportRequiredFieldMissing");
        }

        return value.Trim();
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

    private static decimal? ParseDecimalOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim() == "-")
        {
            return null;
        }

        var normalized = value.Trim();
        if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ||
            decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.CurrentCulture, out parsed))
        {
            return parsed;
        }

        throw new BusinessException("ImportDecimalInvalidFormat");
    }

    private static DateOnly? ParseDateOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim() == "-")
        {
            return null;
        }

        var normalized = value.Trim();
        var formats = new[] { "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy" };
        if (DateOnly.TryParseExact(normalized, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ||
            DateOnly.TryParse(normalized, CultureInfo.CurrentCulture, DateTimeStyles.None, out parsed))
        {
            return parsed;
        }

        throw new BusinessException("ImportDateInvalidFormat");
    }

    private static string AssignmentKey(Guid employeeId, Guid goalId)
        => $"{employeeId:N}|{goalId:N}";

    private static void AddImportRowResult(GoalAssignmentImportResultDto result, int rowNumber, string key, string status, string? message = null)
    {
        result.Rows.Add(new GoalAssignmentImportRowResultDto
        {
            RowNumber = rowNumber,
            Key = key,
            Status = status,
            Message = message
        });
    }

    private class GoalAssignmentImportRow
    {
        public int RowNumber { get; set; }
        public string EmployeeMatricola { get; set; } = string.Empty;
        public string GoalLookup { get; set; } = string.Empty;
        public string? WeightText { get; set; }
        public string? TargetValueText { get; set; }
        public string? StartDateText { get; set; }
        public string? DueDateText { get; set; }
        public string Status { get; set; } = "Draft";
        public Guid? EmployeeId { get; set; }
        public Guid? GoalId { get; set; }
        public decimal? Weight { get; set; }
        public decimal? TargetValue { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? DueDate { get; set; }
        public string? AssignmentKey { get; set; }
    }
}
