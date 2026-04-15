using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SuccessFactor.Cycles;
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

    public AdminGoalAssignmentAppService(
        ICurrentUser currentUser,
        IAsyncQueryableExecuter asyncExecuter,
        IRepository<Cycle, Guid> cycleRepository,
        IRepository<CycleParticipant, Guid> participantRepository,
        IRepository<Employee, Guid> employeeRepository,
        IRepository<Goal, Guid> goalRepository,
        IRepository<GoalAssignment, Guid> assignmentRepository,
        IRepository<ProcessTemplate, Guid> templateRepository,
        IRepository<ProcessPhase, Guid> phaseRepository)
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

        if (!roles.Any(x => x.Contains("admin", StringComparison.OrdinalIgnoreCase)))
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
}
