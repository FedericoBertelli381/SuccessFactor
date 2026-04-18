using Microsoft.AspNetCore.Authorization;
using SuccessFactor.Competencies;
using SuccessFactor.Competencies.Assessments;
using SuccessFactor.Competencies.Models;
using SuccessFactor.Cycles;
using SuccessFactor.Employees;
using SuccessFactor.Goals;
using SuccessFactor.My.Dtos;
using SuccessFactor.My.Support;
using SuccessFactor.Security;
using SuccessFactor.Workflow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Users;

namespace SuccessFactor.Hr;

[Authorize]
public class HrDashboardAppService : ApplicationService, IHrDashboardAppService
{
    private readonly ICurrentUser _currentUser;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IRepository<Cycle, Guid> _cycleRepository;
    private readonly IRepository<CycleParticipant, Guid> _cycleParticipantRepository;
    private readonly IRepository<Employee, Guid> _employeeRepository;
    private readonly IRepository<ProcessPhase, Guid> _processPhaseRepository;
    private readonly IPhasePermissionResolver _phasePermissionResolver;
    private readonly IRepository<GoalAssignment, Guid> _goalAssignmentRepository;
    private readonly IRepository<Goal, Guid> _goalRepository;
    private readonly IRepository<GoalProgressEntry, Guid> _goalProgressEntryRepository;
    private readonly IRepository<CompetencyAssessment, Guid> _assessmentRepository;
    private readonly IRepository<CompetencyAssessmentItem, Guid> _assessmentItemRepository;
    private readonly IRepository<CompetencyModel, Guid> _modelRepository;
    private readonly IRepository<CompetencyModelItem, Guid> _modelItemRepository;
    private readonly IRepository<Competency, Guid> _competencyRepository;

    private const string GoalFieldProgressPercent = "Goals.ProgressPercent";
    private const string GoalFieldActualValue = "Goals.ActualValue";
    private const string GoalFieldNote = "Goals.Note";
    private const string GoalFieldAttachmentId = "Goals.AttachmentId";
    private const string FieldScore = "Competencies.Score";
    private const string FieldComment = "Competencies.Comment";
    private const string FieldEvidenceAttachmentId = "Competencies.EvidenceAttachmentId";

    public HrDashboardAppService(
        ICurrentUser currentUser,
        IAsyncQueryableExecuter asyncExecuter,
        IRepository<Cycle, Guid> cycleRepository,
        IRepository<CycleParticipant, Guid> cycleParticipantRepository,
        IRepository<Employee, Guid> employeeRepository,
        IRepository<ProcessPhase, Guid> processPhaseRepository,
        IPhasePermissionResolver phasePermissionResolver,
        IRepository<GoalAssignment, Guid> goalAssignmentRepository,
        IRepository<Goal, Guid> goalRepository,
        IRepository<GoalProgressEntry, Guid> goalProgressEntryRepository,
        IRepository<CompetencyAssessment, Guid> assessmentRepository,
        IRepository<CompetencyAssessmentItem, Guid> assessmentItemRepository,
        IRepository<CompetencyModel, Guid> modelRepository,
        IRepository<CompetencyModelItem, Guid> modelItemRepository,
        IRepository<Competency, Guid> competencyRepository)
    {
        _currentUser = currentUser;
        _asyncExecuter = asyncExecuter;
        _cycleRepository = cycleRepository;
        _cycleParticipantRepository = cycleParticipantRepository;
        _employeeRepository = employeeRepository;
        _processPhaseRepository = processPhaseRepository;
        _phasePermissionResolver = phasePermissionResolver;
        _goalAssignmentRepository = goalAssignmentRepository;
        _goalRepository = goalRepository;
        _goalProgressEntryRepository = goalProgressEntryRepository;
        _assessmentRepository = assessmentRepository;
        _assessmentItemRepository = assessmentItemRepository;
        _modelRepository = modelRepository;
        _modelItemRepository = modelItemRepository;
        _competencyRepository = competencyRepository;
    }

    public async Task<HrDashboardDto> GetAsync(GetHrDashboardInput input)
    {
        EnsureCurrentUserIsHr();

        input ??= new GetHrDashboardInput();

        var cycleQuery = await _cycleRepository.GetQueryableAsync();
        var cycles = await _asyncExecuter.ToListAsync(
            cycleQuery
                .OrderByDescending(x => x.CycleYear)
                .ThenByDescending(x => x.CreationTime)
                .ThenBy(x => x.Name));

        var selectedCycle = ResolveSelectedCycle(cycles, input.CycleId);

        var dto = new HrDashboardDto
        {
            SelectedCycleId = selectedCycle?.Id,
            SelectedCycleName = selectedCycle?.Name,
            SelectedCycleStatus = selectedCycle?.Status,
            Cycles = cycles
                .Select(x => new HrCycleLookupDto
                {
                    CycleId = x.Id,
                    CycleName = x.Name,
                    CycleStatus = x.Status,
                    IsSelected = selectedCycle is not null && x.Id == selectedCycle.Id
                })
                .ToList()
        };

        if (selectedCycle is null)
        {
            return dto;
        }

        var selectedEmployeeId = input.TargetEmployeeId;
        var participantQuery = await _cycleParticipantRepository.GetQueryableAsync();
        var participants = await _asyncExecuter.ToListAsync(
            participantQuery
                .Where(x => x.CycleId == selectedCycle.Id)
                .OrderBy(x => x.EmployeeId));

        if (!selectedEmployeeId.HasValue && participants.Count > 0)
        {
            selectedEmployeeId = participants[0].EmployeeId;
        }

        if (selectedEmployeeId.HasValue && participants.All(x => x.EmployeeId != selectedEmployeeId.Value))
        {
            throw new BusinessException("HrTargetEmployeeNotInCycle");
        }

        var employeeIds = participants
            .Select(x => x.EmployeeId)
            .Distinct()
            .ToList();

        var phaseIds = participants
            .Where(x => x.CurrentPhaseId.HasValue)
            .Select(x => x.CurrentPhaseId!.Value)
            .Distinct()
            .ToList();

        var employees = new List<Employee>();

        if (employeeIds.Count > 0)
        {
            var employeeQuery = await _employeeRepository.GetQueryableAsync();
            employees = await _asyncExecuter.ToListAsync(
                employeeQuery.Where(x => employeeIds.Contains(x.Id)));
        }

        var phases = new List<ProcessPhase>();

        if (phaseIds.Count > 0)
        {
            var phaseQuery = await _processPhaseRepository.GetQueryableAsync();
            phases = await _asyncExecuter.ToListAsync(
                phaseQuery.Where(x => phaseIds.Contains(x.Id)));
        }

        var employeeById = employees.ToDictionary(x => x.Id, x => x);
        var phaseById = phases.ToDictionary(x => x.Id, x => x);

        dto.Participants = participants
            .Select(x =>
            {
                employeeById.TryGetValue(x.EmployeeId, out var employee);

                ProcessPhase? phase = null;
                if (x.CurrentPhaseId.HasValue)
                {
                    phaseById.TryGetValue(x.CurrentPhaseId.Value, out phase);
                }

                return new HrCycleParticipantDto
                {
                    EmployeeId = x.EmployeeId,
                    Matricola = employee?.Matricola ?? string.Empty,
                    FullName = employee?.FullName ?? string.Empty,
                    Email = employee?.Email,
                    ParticipantStatus = x.Status,
                    CurrentPhaseId = x.CurrentPhaseId,
                    CurrentPhaseCode = phase?.Code,
                    CurrentPhaseName = phase?.Name
                };
            })
            .OrderBy(x => x.FullName)
            .ToList();

        dto.TotalParticipants = dto.Participants.Count;
        dto.ActiveParticipants = dto.Participants.Count(x =>
            string.Equals(x.ParticipantStatus, "Active", StringComparison.OrdinalIgnoreCase));
        dto.CompletedParticipants = dto.Participants.Count(x =>
            string.Equals(x.ParticipantStatus, "Completed", StringComparison.OrdinalIgnoreCase));

        if (!selectedEmployeeId.HasValue)
        {
            return dto;
        }

        var selectedParticipant = participants.First(x => x.EmployeeId == selectedEmployeeId.Value);
        employeeById.TryGetValue(selectedEmployeeId.Value, out var selectedEmployee);

        ProcessPhase? selectedPhase = null;
        if (selectedParticipant.CurrentPhaseId.HasValue)
        {
            phaseById.TryGetValue(selectedParticipant.CurrentPhaseId.Value, out selectedPhase);
        }

        dto.SelectedEmployeeId = selectedEmployeeId.Value;
        dto.SelectedEmployeeName = selectedEmployee?.FullName;
        dto.SelectedEmployeePhaseCode = selectedPhase?.Code;
        dto.Goals = await BuildGoalsAsync(selectedCycle, selectedParticipant, selectedEmployee, selectedPhase);
        dto.Assessments = await BuildAssessmentsAsync(selectedCycle, selectedParticipant, selectedEmployee, selectedPhase, employeeById);

        return dto;
    }

    private async Task<MyGoalsDto> BuildGoalsAsync(
        Cycle selectedCycle,
        CycleParticipant selectedParticipant,
        Employee? selectedEmployee,
        ProcessPhase? selectedPhase)
    {
        if (!selectedParticipant.CurrentPhaseId.HasValue)
        {
            return new MyGoalsDto
            {
                EmployeeId = selectedParticipant.EmployeeId,
                EmployeeName = selectedEmployee?.FullName ?? string.Empty,
                CycleId = selectedCycle.Id,
                CycleName = selectedCycle.Name,
                CurrentPhaseId = null,
                CurrentPhaseCode = selectedPhase?.Code,
                RoleCodeUsed = "HR",
                CanEdit = false
            };
        }

        var phasePermission = await _phasePermissionResolver.GetEffectivePhasePermissionAsync(
            selectedCycle.TemplateId,
            selectedParticipant.CurrentPhaseId!.Value,
            "HR");

        var goalFieldAccess = await _phasePermissionResolver.GetEffectiveFieldAccessAsync(
            selectedCycle.TemplateId,
            selectedParticipant.CurrentPhaseId.Value,
            "HR",
            GoalFieldProgressPercent,
            GoalFieldActualValue,
            GoalFieldNote,
            GoalFieldAttachmentId);

        var assignmentQuery = await _goalAssignmentRepository.GetQueryableAsync();
        var goalQuery = await _goalRepository.GetQueryableAsync();

        var rows = await _asyncExecuter.ToListAsync(
            from assignment in assignmentQuery
            join goal in goalQuery on assignment.GoalId equals goal.Id
            where assignment.CycleId == selectedCycle.Id
               && assignment.EmployeeId == selectedParticipant.EmployeeId
            orderby goal.Title
            select new
            {
                Assignment = assignment,
                Goal = goal
            });

        var assignmentIds = rows.Select(x => x.Assignment.Id).Distinct().ToList();
        var progressEntries = new List<GoalProgressEntry>();

        if (assignmentIds.Count > 0)
        {
            var progressQuery = await _goalProgressEntryRepository.GetQueryableAsync();
            progressEntries = await _asyncExecuter.ToListAsync(
                progressQuery.Where(x => assignmentIds.Contains(x.AssignmentId)));
        }

        var progressByAssignment = progressEntries
            .GroupBy(x => x.AssignmentId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.EntryDate).ToList());

        var canEdit = phasePermission?.CanEdit ?? false;

        return new MyGoalsDto
        {
            EmployeeId = selectedParticipant.EmployeeId,
            EmployeeName = selectedEmployee?.FullName ?? string.Empty,
            CycleId = selectedCycle.Id,
            CycleName = selectedCycle.Name,
            CurrentPhaseId = selectedParticipant.CurrentPhaseId,
            CurrentPhaseCode = selectedPhase?.Code,
            RoleCodeUsed = "HR",
            CanEdit = canEdit,
            Items = rows.Select(x =>
            {
                progressByAssignment.TryGetValue(x.Assignment.Id, out var entries);
                entries ??= new List<GoalProgressEntry>();

                var last = entries.FirstOrDefault();

                return new MyGoalItemDto
                {
                    AssignmentId = x.Assignment.Id,
                    GoalId = x.Goal.Id,
                    GoalName = x.Goal.Title,
                    Weight = x.Assignment.Weight,
                    Status = x.Assignment.Status,
                    TargetValue = x.Assignment.TargetValue,
                    DueDate = x.Assignment.DueDate,
                    CanEdit = canEdit,
                    ProgressPercentAccess = goalFieldAccess[GoalFieldProgressPercent],
                    ActualValueAccess = goalFieldAccess[GoalFieldActualValue],
                    NoteAccess = goalFieldAccess[GoalFieldNote],
                    AttachmentAccess = goalFieldAccess[GoalFieldAttachmentId],
                    LastProgress = last is null
                        ? null
                        : new MyGoalLastProgressDto
                        {
                            EntryDate = last.EntryDate,
                            ProgressPercent = last.ProgressPercent,
                            ActualValue = last.ActualValue,
                            Note = last.Note,
                            AttachmentId = last.AttachmentId
                        },
                    Summary = new MyGoalProgressSummaryDto
                    {
                        EntriesCount = entries.Count,
                        LastEntryDate = last?.EntryDate,
                        LastProgressPercent = last?.ProgressPercent,
                        LastActualValue = last?.ActualValue
                    }
                };
            }).ToList()
        };
    }

    private async Task<MyAssessmentsDto> BuildAssessmentsAsync(
        Cycle selectedCycle,
        CycleParticipant selectedParticipant,
        Employee? selectedEmployee,
        ProcessPhase? selectedPhase,
        Dictionary<Guid, Employee> employeeById)
    {
        if (!selectedParticipant.CurrentPhaseId.HasValue)
        {
            return new MyAssessmentsDto
            {
                EmployeeId = selectedParticipant.EmployeeId,
                EmployeeName = selectedEmployee?.FullName ?? string.Empty,
                CycleId = selectedCycle.Id,
                CycleName = selectedCycle.Name,
                CurrentPhaseId = null,
                CurrentPhaseCode = selectedPhase?.Code,
                RoleCodeUsed = "HR",
                CanEdit = false,
                CanSubmitAny = false
            };
        }

        var phasePermission = await _phasePermissionResolver.GetEffectivePhasePermissionAsync(
            selectedCycle.TemplateId,
            selectedParticipant.CurrentPhaseId!.Value,
            "HR");

        var fieldAccess = await _phasePermissionResolver.GetEffectiveFieldAccessAsync(
            selectedCycle.TemplateId,
            selectedParticipant.CurrentPhaseId.Value,
            "HR",
            FieldScore,
            FieldComment,
            FieldEvidenceAttachmentId);

        var assessmentQuery = await _assessmentRepository.GetQueryableAsync();
        var assessments = await _asyncExecuter.ToListAsync(
            assessmentQuery
                .Where(x => x.CycleId == selectedCycle.Id && x.EmployeeId == selectedParticipant.EmployeeId)
                .OrderBy(x => x.AssessmentType));

        var assessmentIds = assessments.Select(x => x.Id).ToList();
        var modelIds = assessments
            .Where(x => x.ModelId.HasValue)
            .Select(x => x.ModelId!.Value)
            .Distinct()
            .ToList();

        var assessmentItems = new List<CompetencyAssessmentItem>();
        if (assessmentIds.Count > 0)
        {
            var assessmentItemQuery = await _assessmentItemRepository.GetQueryableAsync();
            assessmentItems = await _asyncExecuter.ToListAsync(
                assessmentItemQuery.Where(x => assessmentIds.Contains(x.AssessmentId)));
        }

        var assessmentItemsByAssessmentId = assessmentItems
            .GroupBy(x => x.AssessmentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var models = new List<CompetencyModel>();
        if (modelIds.Count > 0)
        {
            var modelQuery = await _modelRepository.GetQueryableAsync();
            models = await _asyncExecuter.ToListAsync(modelQuery.Where(x => modelIds.Contains(x.Id)));
        }

        var modelById = models.ToDictionary(x => x.Id, x => x);

        var modelItems = new List<CompetencyModelItem>();
        if (modelIds.Count > 0)
        {
            var modelItemQuery = await _modelItemRepository.GetQueryableAsync();
            modelItems = await _asyncExecuter.ToListAsync(
                modelItemQuery.Where(x => modelIds.Contains(x.ModelId)));
        }

        var modelItemsByModelId = modelItems
            .GroupBy(x => x.ModelId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var competencyIds = assessmentItems
            .Select(x => x.CompetencyId)
            .Concat(modelItems.Select(x => x.CompetencyId))
            .Distinct()
            .ToList();

        var competencies = new List<Competency>();
        if (competencyIds.Count > 0)
        {
            var competencyQuery = await _competencyRepository.GetQueryableAsync();
            competencies = await _asyncExecuter.ToListAsync(
                competencyQuery.Where(x => competencyIds.Contains(x.Id)));
        }

        var competencyById = competencies.ToDictionary(x => x.Id, x => x);
        var canEdit = phasePermission?.CanEdit ?? false;
        var canSubmitMacro = phasePermission?.CanSubmit ?? false;

        var items = assessments.Select(assessment =>
        {
            assessmentItemsByAssessmentId.TryGetValue(assessment.Id, out var itemsForAssessment);
            itemsForAssessment ??= new List<CompetencyAssessmentItem>();

            CompetencyModel? model = null;
            List<CompetencyModelItem> modelItemsForAssessment = new();

            if (assessment.ModelId.HasValue)
            {
                modelById.TryGetValue(assessment.ModelId.Value, out model);
                if (modelItemsByModelId.TryGetValue(assessment.ModelId.Value, out var tmpModelItems))
                {
                    modelItemsForAssessment = tmpModelItems;
                }
            }

            var itemByCompetencyId = itemsForAssessment
                .GroupBy(x => x.CompetencyId)
                .ToDictionary(g => g.Key, g => g.First());

            var competencyDtos = modelItemsForAssessment
                .OrderBy(x => x.CompetencyId)
                .Select(modelItem =>
                {
                    competencyById.TryGetValue(modelItem.CompetencyId, out var competency);
                    itemByCompetencyId.TryGetValue(modelItem.CompetencyId, out var currentItem);

                    return new MyAssessmentCompetencyItemDto
                    {
                        CompetencyId = modelItem.CompetencyId,
                        CompetencyCode = competency?.Code ?? string.Empty,
                        CompetencyName = competency?.Name ?? string.Empty,
                        IsRequired = modelItem.IsRequired,
                        Weight = modelItem.Weight,
                        Score = currentItem?.Score,
                        Comment = currentItem?.Comment,
                        EvidenceAttachmentId = currentItem?.EvidenceAttachmentId
                    };
                })
                .ToList();

            var requiredItemsCount = competencyDtos.Count(x => x.IsRequired);
            var missingRequiredCount = competencyDtos.Count(x => x.IsRequired && !x.Score.HasValue);
            var isDraft = string.Equals(assessment.Status, "Draft", StringComparison.OrdinalIgnoreCase);

            employeeById.TryGetValue(assessment.EvaluatorEmployeeId, out var evaluatorEmployee);

            return new MyAssessmentItemDto
            {
                CycleId = selectedCycle.Id,
                EmployeeId = selectedParticipant.EmployeeId,
                RoleCodeUsed = "HR",
                CurrentPhaseId = selectedParticipant.CurrentPhaseId,
                CurrentPhaseCode = selectedPhase?.Code,
                CurrentPhaseName = selectedPhase?.Name,
                AssessmentId = assessment.Id,
                AssessmentType = assessment.AssessmentType,
                Status = assessment.Status,
                TargetEmployeeId = selectedParticipant.EmployeeId,
                TargetEmployeeName = selectedEmployee?.FullName ?? string.Empty,
                EvaluatorEmployeeId = assessment.EvaluatorEmployeeId,
                EvaluatorEmployeeName = evaluatorEmployee?.FullName ?? string.Empty,
                ModelId = assessment.ModelId,
                ModelName = model?.Name,
                MinScore = model?.MinScore ?? 0,
                MaxScore = model?.MaxScore ?? 0,
                CanEdit = canEdit && isDraft,
                CanSubmit = canSubmitMacro && isDraft && missingRequiredCount == 0,
                ScoreAccess = fieldAccess[FieldScore],
                CommentAccess = fieldAccess[FieldComment],
                EvidenceAttachmentAccess = fieldAccess[FieldEvidenceAttachmentId],
                ItemsCount = competencyDtos.Count,
                RequiredItemsCount = requiredItemsCount,
                MissingRequiredCount = missingRequiredCount,
                Items = competencyDtos
            };
        }).ToList();

        return new MyAssessmentsDto
        {
            EmployeeId = selectedParticipant.EmployeeId,
            EmployeeName = selectedEmployee?.FullName ?? string.Empty,
            CycleId = selectedCycle.Id,
            CycleName = selectedCycle.Name,
            CurrentPhaseId = selectedParticipant.CurrentPhaseId,
            CurrentPhaseCode = selectedPhase?.Code,
            RoleCodeUsed = "HR",
            CanEdit = canEdit,
            CanSubmitAny = items.Any(x => x.CanSubmit),
            Items = items
        };
    }

    private void EnsureCurrentUserIsHr()
    {
        var roles = _currentUser.Roles ?? Array.Empty<string>();

        if (!SuccessFactorRoles.IsAdminOrHr(roles))
        {
            throw new BusinessException("CurrentUserIsNotHr");
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
                throw new BusinessException("HrCycleNotFound");
            }

            return selected;
        }

        return cycles
            .OrderByDescending(x => string.Equals(x.Status, "Active", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(x => x.CycleYear)
            .ThenByDescending(x => x.CreationTime)
            .First();
    }
}
