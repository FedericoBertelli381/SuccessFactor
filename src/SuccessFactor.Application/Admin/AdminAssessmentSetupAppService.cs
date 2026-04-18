using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SuccessFactor.Competencies.Models;
using SuccessFactor.Security;
using SuccessFactor.Competencies.Assessments;
using SuccessFactor.Cycles;
using SuccessFactor.Employees;
using SuccessFactor.Process;
using SuccessFactor.Workflow;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Uow;
using Volo.Abp.Users;

namespace SuccessFactor.Admin;

[Authorize]
public class AdminAssessmentSetupAppService : ApplicationService, IAdminAssessmentSetupAppService
{
    private static readonly string[] AllowedAssessmentTypes = ["Self", "Manager", "Peer", "HR"];

    private readonly ICurrentUser _currentUser;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IRepository<Cycle, Guid> _cycleRepository;
    private readonly IRepository<CycleParticipant, Guid> _participantRepository;
    private readonly IRepository<Employee, Guid> _employeeRepository;
    private readonly IRepository<CompetencyModel, Guid> _modelRepository;
    private readonly IRepository<CompetencyModelItem, Guid> _modelItemRepository;
    private readonly IRepository<CompetencyAssessment, Guid> _assessmentRepository;
    private readonly IRepository<CompetencyAssessmentItem, Guid> _assessmentItemRepository;
    private readonly IRepository<ProcessTemplate, Guid> _templateRepository;
    private readonly IRepository<ProcessPhase, Guid> _phaseRepository;

    public AdminAssessmentSetupAppService(
        ICurrentUser currentUser,
        IAsyncQueryableExecuter asyncExecuter,
        IRepository<Cycle, Guid> cycleRepository,
        IRepository<CycleParticipant, Guid> participantRepository,
        IRepository<Employee, Guid> employeeRepository,
        IRepository<CompetencyModel, Guid> modelRepository,
        IRepository<CompetencyModelItem, Guid> modelItemRepository,
        IRepository<CompetencyAssessment, Guid> assessmentRepository,
        IRepository<CompetencyAssessmentItem, Guid> assessmentItemRepository,
        IRepository<ProcessTemplate, Guid> templateRepository,
        IRepository<ProcessPhase, Guid> phaseRepository)
    {
        _currentUser = currentUser;
        _asyncExecuter = asyncExecuter;
        _cycleRepository = cycleRepository;
        _participantRepository = participantRepository;
        _employeeRepository = employeeRepository;
        _modelRepository = modelRepository;
        _modelItemRepository = modelItemRepository;
        _assessmentRepository = assessmentRepository;
        _assessmentItemRepository = assessmentItemRepository;
        _templateRepository = templateRepository;
        _phaseRepository = phaseRepository;
    }

    public async Task<AssessmentSetupAdminDto> GetAsync(Guid? cycleId = null)
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
            phases = await _asyncExecuter.ToListAsync(phaseQuery.Where(x => x.TemplateId == selectedCycle.TemplateId));
        }

        var phaseById = phases.ToDictionary(x => x.Id, x => x);

        var modelQuery = await _modelRepository.GetQueryableAsync();
        var models = await _asyncExecuter.ToListAsync(modelQuery.OrderBy(x => x.Name));
        var modelItems = await _modelItemRepository.GetListAsync();
        var modelById = models.ToDictionary(x => x.Id, x => x);

        var participants = new List<CycleParticipant>();
        var assessments = new List<CompetencyAssessment>();
        var assessmentItems = new List<CompetencyAssessmentItem>();

        if (selectedCycle is not null)
        {
            var participantQuery = await _participantRepository.GetQueryableAsync();
            participants = await _asyncExecuter.ToListAsync(
                participantQuery
                    .Where(x => x.CycleId == selectedCycle.Id)
                    .OrderBy(x => x.EmployeeId));

            var assessmentQuery = await _assessmentRepository.GetQueryableAsync();
            assessments = await _asyncExecuter.ToListAsync(
                assessmentQuery
                    .Where(x => x.CycleId == selectedCycle.Id)
                    .OrderBy(x => x.EmployeeId));

            var assessmentIds = assessments.Select(x => x.Id).ToList();
            var itemQuery = await _assessmentItemRepository.GetQueryableAsync();
            assessmentItems = await _asyncExecuter.ToListAsync(itemQuery.Where(x => assessmentIds.Contains(x.AssessmentId)));
        }

        var employeeIds = participants.Select(x => x.EmployeeId)
            .Concat(assessments.Select(x => x.EmployeeId))
            .Concat(assessments.Select(x => x.EvaluatorEmployeeId))
            .Distinct()
            .ToList();

        var activeEmployeeQuery = await _employeeRepository.GetQueryableAsync();
        var activeEmployees = await _asyncExecuter.ToListAsync(
            activeEmployeeQuery
                .Where(x => x.IsActive || employeeIds.Contains(x.Id))
                .OrderBy(x => x.Matricola)
                .ThenBy(x => x.FullName));

        var employeeById = activeEmployees.ToDictionary(x => x.Id, x => x);

        return new AssessmentSetupAdminDto
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
            Evaluators = activeEmployees
                .Where(x => x.IsActive)
                .Select(MapEmployee)
                .ToList(),
            Models = MapModels(models, modelItems, assessments),
            Assessments = assessments
                .Select(x => MapAssessment(x, employeeById, modelById, modelItems, assessmentItems))
                .OrderBy(x => x.Matricola)
                .ThenBy(x => x.EmployeeName)
                .ThenBy(x => x.AssessmentType)
                .ThenBy(x => x.EvaluatorName)
                .ToList()
        };
    }

    public async Task<AssessmentSetupAdminListItemDto> GenerateAsync(GenerateAssessmentSetupInput input)
    {
        EnsureTenantAndAdmin();
        NormalizeAndValidate(input);

        var cycle = await _cycleRepository.GetAsync(input.CycleId);
        EnsureCycleEditable(cycle);

        await ValidateParticipantAsync(input);
        await ValidateEvaluatorAsync(input);

        var model = await _modelRepository.GetAsync(input.ModelId);
        var modelItems = await _modelItemRepository.GetListAsync(x => x.ModelId == model.Id);
        if (modelItems.Count == 0)
        {
            throw new BusinessException("CompetencyModelHasNoItems");
        }

        var assessments = await _assessmentRepository.GetListAsync(x =>
            x.CycleId == input.CycleId &&
            x.EmployeeId == input.EmployeeId &&
            x.EvaluatorEmployeeId == input.EvaluatorEmployeeId &&
            x.AssessmentType == input.AssessmentType);

        CompetencyAssessment assessment;
        if (assessments.Count > 0)
        {
            assessment = assessments.OrderBy(x => x.CreationTime).First();
            await SafeRegenerateDraftAsync(assessment, model, modelItems, input.SafeRegenerateDraft);
        }
        else
        {
            assessment = new CompetencyAssessment
            {
                TenantId = CurrentTenant.Id,
                CycleId = input.CycleId,
                EmployeeId = input.EmployeeId,
                EvaluatorEmployeeId = input.EvaluatorEmployeeId,
                ModelId = input.ModelId,
                AssessmentType = input.AssessmentType,
                Status = "Draft"
            };

            assessment = await _assessmentRepository.InsertAsync(assessment, autoSave: false);
            await SaveCurrentUnitOfWorkAsync();
            await InsertMissingItemsAsync(assessment.Id, modelItems, []);
        }

        await SaveCurrentUnitOfWorkAsync();

        var employee = await _employeeRepository.GetAsync(assessment.EmployeeId);
        var evaluator = await _employeeRepository.GetAsync(assessment.EvaluatorEmployeeId);
        var savedItems = await _assessmentItemRepository.GetListAsync(x => x.AssessmentId == assessment.Id);

        return MapAssessment(
            assessment,
            new Dictionary<Guid, Employee>
            {
                [employee.Id] = employee,
                [evaluator.Id] = evaluator
            },
            new Dictionary<Guid, CompetencyModel> { [model.Id] = model },
            modelItems,
            savedItems);
    }

    private async Task SafeRegenerateDraftAsync(
        CompetencyAssessment assessment,
        CompetencyModel model,
        List<CompetencyModelItem> modelItems,
        bool safeRegenerateDraft)
    {
        if (!IsDraft(assessment))
        {
            throw new BusinessException("OnlyDraftAssessmentsCanBeRegenerated");
        }

        if (!safeRegenerateDraft)
        {
            throw new BusinessException("AssessmentAlreadyExists");
        }

        var existingItems = await _assessmentItemRepository.GetListAsync(x => x.AssessmentId == assessment.Id);
        assessment.ModelId = model.Id;
        await _assessmentRepository.UpdateAsync(assessment, autoSave: false);
        await InsertMissingItemsAsync(assessment.Id, modelItems, existingItems.Select(x => x.CompetencyId).ToHashSet());
    }

    private async Task InsertMissingItemsAsync(
        Guid assessmentId,
        List<CompetencyModelItem> modelItems,
        HashSet<Guid> existingCompetencyIds)
    {
        foreach (var modelItem in modelItems.Where(x => !existingCompetencyIds.Contains(x.CompetencyId)))
        {
            await _assessmentItemRepository.InsertAsync(new CompetencyAssessmentItem
            {
                TenantId = CurrentTenant.Id,
                AssessmentId = assessmentId,
                CompetencyId = modelItem.CompetencyId
            }, autoSave: false);
        }
    }

    private async Task SaveCurrentUnitOfWorkAsync()
    {
        if (CurrentUnitOfWork is null)
        {
            throw new BusinessException("UnitOfWorkMissing");
        }

        await CurrentUnitOfWork.SaveChangesAsync();
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

    private async Task ValidateParticipantAsync(GenerateAssessmentSetupInput input)
    {
        if (!await _participantRepository.AnyAsync(x =>
                x.CycleId == input.CycleId &&
                x.EmployeeId == input.EmployeeId &&
                x.Status != "Excluded"))
        {
            throw new BusinessException("EmployeeIsNotActiveCycleParticipant");
        }

        if (!await _employeeRepository.AnyAsync(x => x.Id == input.EmployeeId && x.IsActive))
        {
            throw new BusinessException("EmployeeNotFoundOrInactive");
        }
    }

    private async Task ValidateEvaluatorAsync(GenerateAssessmentSetupInput input)
    {
        if (string.Equals(input.AssessmentType, "Self", StringComparison.OrdinalIgnoreCase) &&
            input.EvaluatorEmployeeId != input.EmployeeId)
        {
            throw new BusinessException("SelfAssessmentEvaluatorMustBeTargetEmployee");
        }

        if (!await _employeeRepository.AnyAsync(x => x.Id == input.EvaluatorEmployeeId && x.IsActive))
        {
            throw new BusinessException("EvaluatorNotFoundOrInactive");
        }
    }

    private static void NormalizeAndValidate(GenerateAssessmentSetupInput input)
    {
        if (input.CycleId == Guid.Empty)
        {
            throw new BusinessException("CycleIdRequired");
        }

        if (input.EmployeeId == Guid.Empty)
        {
            throw new BusinessException("EmployeeIdRequired");
        }

        if (input.EvaluatorEmployeeId == Guid.Empty)
        {
            throw new BusinessException("EvaluatorEmployeeIdRequired");
        }

        if (input.ModelId == Guid.Empty)
        {
            throw new BusinessException("CompetencyModelIdRequired");
        }

        input.AssessmentType = string.IsNullOrWhiteSpace(input.AssessmentType)
            ? "Manager"
            : input.AssessmentType.Trim();

        if (!AllowedAssessmentTypes.Contains(input.AssessmentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new BusinessException("AssessmentTypeInvalid");
        }

        input.AssessmentType = AllowedAssessmentTypes.First(x => string.Equals(x, input.AssessmentType, StringComparison.OrdinalIgnoreCase));
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

    private static void EnsureCycleEditable(Cycle cycle)
    {
        if (IsClosed(cycle))
        {
            throw new BusinessException("ClosedCycleAssessmentsCannotBeChanged");
        }
    }

    private static bool IsClosed(Cycle cycle)
        => string.Equals(cycle.Status, "Closed", StringComparison.OrdinalIgnoreCase);

    private static bool IsDraft(CompetencyAssessment assessment)
        => string.Equals(assessment.Status, "Draft", StringComparison.OrdinalIgnoreCase);

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

    private static List<CompetencyModelAdminListItemDto> MapModels(
        List<CompetencyModel> models,
        List<CompetencyModelItem> modelItems,
        List<CompetencyAssessment> assessments)
    {
        var itemGroups = modelItems.GroupBy(x => x.ModelId).ToDictionary(x => x.Key, x => x.ToList());
        var assessmentCountByModelId = assessments
            .Where(x => x.ModelId.HasValue)
            .GroupBy(x => x.ModelId!.Value)
            .ToDictionary(x => x.Key, x => x.Count());

        return models
            .Select(x =>
            {
                itemGroups.TryGetValue(x.Id, out var items);
                items ??= [];

                return new CompetencyModelAdminListItemDto
                {
                    ModelId = x.Id,
                    Name = x.Name,
                    ScaleType = x.ScaleType,
                    MinScore = x.MinScore,
                    MaxScore = x.MaxScore,
                    ItemCount = items.Count,
                    RequiredItemCount = items.Count(i => i.IsRequired),
                    TotalWeight = items.Count == 0 ? null : items.Sum(i => i.Weight ?? 0m),
                    AssessmentCount = assessmentCountByModelId.GetValueOrDefault(x.Id),
                    CanEditStructure = false,
                    CanDelete = false
                };
            })
            .OrderBy(x => x.Name)
            .ToList();
    }

    private static AssessmentSetupAdminListItemDto MapAssessment(
        CompetencyAssessment assessment,
        Dictionary<Guid, Employee> employeeById,
        Dictionary<Guid, CompetencyModel> modelById,
        List<CompetencyModelItem> modelItems,
        List<CompetencyAssessmentItem> assessmentItems)
    {
        employeeById.TryGetValue(assessment.EmployeeId, out var employee);
        employeeById.TryGetValue(assessment.EvaluatorEmployeeId, out var evaluator);

        CompetencyModel? model = null;
        if (assessment.ModelId.HasValue)
        {
            modelById.TryGetValue(assessment.ModelId.Value, out model);
        }

        var items = assessmentItems.Where(x => x.AssessmentId == assessment.Id).ToList();
        var modelCompetencyIds = assessment.ModelId.HasValue
            ? modelItems.Where(x => x.ModelId == assessment.ModelId.Value).Select(x => x.CompetencyId).ToHashSet()
            : [];
        var assessmentCompetencyIds = items.Select(x => x.CompetencyId).ToHashSet();
        var missingModelItemCount = modelCompetencyIds.Count(x => !assessmentCompetencyIds.Contains(x));

        return new AssessmentSetupAdminListItemDto
        {
            AssessmentId = assessment.Id,
            CycleId = assessment.CycleId,
            EmployeeId = assessment.EmployeeId,
            Matricola = employee?.Matricola ?? string.Empty,
            EmployeeName = employee?.FullName ?? string.Empty,
            EvaluatorEmployeeId = assessment.EvaluatorEmployeeId,
            EvaluatorMatricola = evaluator?.Matricola ?? string.Empty,
            EvaluatorName = evaluator?.FullName ?? string.Empty,
            ModelId = assessment.ModelId,
            ModelName = model?.Name ?? string.Empty,
            AssessmentType = assessment.AssessmentType,
            Status = assessment.Status,
            ItemCount = items.Count,
            MissingModelItemCount = missingModelItemCount,
            CanSafeRegenerate = IsDraft(assessment)
        };
    }
}
