using Microsoft.AspNetCore.Authorization;
using SuccessFactor.Competencies;
using SuccessFactor.Competencies.Assessments;
using SuccessFactor.Competencies.Models;
using SuccessFactor.Cycles;
// TODO: sostituisci questi using con i namespace reali delle tue entity
using SuccessFactor.Employees;
using SuccessFactor.My.Dtos;
using SuccessFactor.My.Support;
using SuccessFactor.Workflow;
using SuccessFactor.Workflow.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace SuccessFactor.My;

[Authorize]
public class MyAssessmentsAppService : ApplicationService, IMyAssessmentsAppService
{
    private const string FieldScore = "Competencies.Score";
    private const string FieldComment = "Competencies.Comment";
    private const string FieldEvidenceAttachmentId = "Competencies.EvidenceAttachmentId";

    private readonly IMyWorkflowContextResolver _workflowContextResolver;
    private readonly IPhasePermissionResolver _phasePermissionResolver;

    private readonly IRepository<Employee, Guid> _employeeRepository;
    private readonly IRepository<CompetencyAssessment, Guid> _assessmentRepository;
    private readonly IRepository<CompetencyAssessmentItem, Guid> _assessmentItemRepository;
    private readonly IRepository<CompetencyModel, Guid> _modelRepository;
    private readonly IRepository<CompetencyModelItem, Guid> _modelItemRepository;
    private readonly IRepository<Competency, Guid> _competencyRepository;
    private readonly CompetencyAssessmentAppService _competencyAssessmentAppService;
    private readonly IRepository<CycleParticipant, Guid> _cycleParticipantRepository;
    private readonly IRepository<ProcessPhase, Guid> _processPhaseRepository;

    // usa il tipo reale che hai nel progetto
    private readonly WorkflowAuthorizationService _workflowAuthorizationService;

    public MyAssessmentsAppService(
        IMyWorkflowContextResolver workflowContextResolver,
        IPhasePermissionResolver phasePermissionResolver,
        IRepository<Employee, Guid> employeeRepository,
        IRepository<CompetencyAssessment, Guid> assessmentRepository,
        IRepository<CompetencyAssessmentItem, Guid> assessmentItemRepository,
        IRepository<CompetencyModel, Guid> modelRepository,
        IRepository<CompetencyModelItem, Guid> modelItemRepository,
        IRepository<Competency, Guid> competencyRepository,
        CompetencyAssessmentAppService competencyAssessmentAppService,
        IRepository<CycleParticipant, Guid> cycleParticipantRepository,
        IRepository<ProcessPhase, Guid> processPhaseRepository,
        WorkflowAuthorizationService workflowAuthorizationService)
    {
        _workflowContextResolver = workflowContextResolver;
        _phasePermissionResolver = phasePermissionResolver;
        _employeeRepository = employeeRepository;
        _assessmentRepository = assessmentRepository;
        _assessmentItemRepository = assessmentItemRepository;
        _modelRepository = modelRepository;
        _modelItemRepository = modelItemRepository;
        _competencyRepository = competencyRepository;
        _competencyAssessmentAppService = competencyAssessmentAppService;
        _cycleParticipantRepository = cycleParticipantRepository;
        _processPhaseRepository = processPhaseRepository;
        _workflowAuthorizationService = workflowAuthorizationService;
    }
    public async Task UpsertItemAsync(UpsertAssessmentItemDto input)
    {
        await _competencyAssessmentAppService.UpsertItemAsync(input);
    }

    public async Task SubmitAsync(Guid assessmentId)
    {
        await _competencyAssessmentAppService.SubmitAsync(assessmentId);

    }
    public async Task<MyAssessmentsDto> GetAsync(GetMyAssessmentsInput input)
    {
        input ??= new GetMyAssessmentsInput();

        var context = await _workflowContextResolver.ResolveAsync(input.CycleId);

        var phasePermission = await _phasePermissionResolver.GetEffectivePhasePermissionAsync(
            context.Cycle.TemplateId,
            context.Participant.CurrentPhaseId!.Value,
            context.RoleCodeUsed);

        var fieldAccess = await _phasePermissionResolver.GetEffectiveFieldAccessAsync(
            context.Cycle.TemplateId,
            context.Participant.CurrentPhaseId.Value,
            context.RoleCodeUsed,
            FieldScore,
            FieldComment,
            FieldEvidenceAttachmentId);

        var assessmentQuery = await _assessmentRepository.GetQueryableAsync();

        var assessments = await AsyncExecuter.ToListAsync(
            assessmentQuery
                .Where(x => x.CycleId == context.Cycle.Id && x.EmployeeId == context.Employee.Id)
                .OrderBy(x => x.AssessmentType));

        if (input.OnlyOpen)
        {
            assessments = assessments
                .Where(x => !string.Equals(x.Status, "Closed", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var assessmentEmployeeIds = assessments
            .Select(x => x.EmployeeId)
            .Concat(assessments.Select(x => x.EvaluatorEmployeeId))
            .Distinct()
            .ToList();

        var employees = assessmentEmployeeIds.Count == 0
            ? new List<Employee>()
            : await AsyncExecuter.ToListAsync(
                (await _employeeRepository.GetQueryableAsync())
                    .Where(x => assessmentEmployeeIds.Contains(x.Id)));

        var employeeById = employees
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.First());

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

            assessmentItems = await AsyncExecuter.ToListAsync(
                assessmentItemQuery.Where(x => assessmentIds.Contains(x.AssessmentId)));
        }

        var assessmentItemsByAssessmentId = assessmentItems
            .GroupBy(x => x.AssessmentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var models = new List<CompetencyModel>();

        if (modelIds.Count > 0)
        {
            var modelQuery = await _modelRepository.GetQueryableAsync();

            models = await AsyncExecuter.ToListAsync(
                modelQuery.Where(x => modelIds.Contains(x.Id)));
        }

        var modelById = models.ToDictionary(x => x.Id, x => x);

        var modelItems = new List<CompetencyModelItem>();

        if (modelIds.Count > 0)
        {
            var modelItemQuery = await _modelItemRepository.GetQueryableAsync();

            modelItems = await AsyncExecuter.ToListAsync(
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

            competencies = await AsyncExecuter.ToListAsync(
                competencyQuery.Where(x => competencyIds.Contains(x.Id)));
        }

        var competencyById = competencies.ToDictionary(x => x.Id, x => x);

        var canEdit = phasePermission?.CanEdit ?? false;
        var canSubmitMacro = phasePermission?.CanSubmit ?? false;

        var dtoItems = new List<MyAssessmentItemDto>();

        foreach (var assessment in assessments)
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

            var competencyDtos = new List<MyAssessmentCompetencyItemDto>();

            foreach (var modelItem in modelItemsForAssessment.OrderBy(x => x.CompetencyId))
            {
                competencyById.TryGetValue(modelItem.CompetencyId, out var competency);
                itemByCompetencyId.TryGetValue(modelItem.CompetencyId, out var currentItem);

                competencyDtos.Add(new MyAssessmentCompetencyItemDto
                {
                    CompetencyId = modelItem.CompetencyId,
                    CompetencyCode = competency?.Code ?? string.Empty,
                    CompetencyName = competency?.Name ?? string.Empty,
                    IsRequired = modelItem.IsRequired,
                    Weight = modelItem.Weight,
                    Score = currentItem?.Score,
                    Comment = currentItem?.Comment,
                    EvidenceAttachmentId = currentItem?.EvidenceAttachmentId
                });
            }

            var requiredItemsCount = competencyDtos.Count(x => x.IsRequired);
            var missingRequiredCount = competencyDtos.Count(x => x.IsRequired && !x.Score.HasValue);

            employeeById.TryGetValue(assessment.EmployeeId, out var targetEmployee);
            employeeById.TryGetValue(assessment.EvaluatorEmployeeId, out var evaluatorEmployee);

            var isDraft = string.Equals(assessment.Status, "Draft", StringComparison.OrdinalIgnoreCase);

            dtoItems.Add(new MyAssessmentItemDto
            {
                CycleId = context.Cycle.Id,
                EmployeeId = context.Employee.Id,
                RoleCodeUsed = context.RoleCodeUsed,

                CurrentPhaseId = context.Participant.CurrentPhaseId,
                CurrentPhaseCode = context.CurrentPhase?.Code,
                CurrentPhaseName = context.CurrentPhase?.Name,

                AssessmentId = assessment.Id,
                AssessmentType = assessment.AssessmentType,
                Status = assessment.Status,
                TargetEmployeeId = assessment.EmployeeId,
                TargetEmployeeName = targetEmployee?.FullName ?? string.Empty,
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
            });
        }

        return new MyAssessmentsDto
        {
            EmployeeId = context.Employee.Id,
            EmployeeName = context.Employee.FullName,
            CycleId = context.Cycle.Id,
            CycleName = context.Cycle.Name,
            CurrentPhaseId = context.Participant.CurrentPhaseId,
            CurrentPhaseCode = context.CurrentPhase?.Code,
            RoleCodeUsed = context.RoleCodeUsed,
            CanEdit = canEdit,
            CanSubmitAny = dtoItems.Any(x => x.CanSubmit),
            Items = dtoItems
        };
    }
}
