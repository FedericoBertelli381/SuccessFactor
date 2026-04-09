using Microsoft.AspNetCore.Authorization;
using SuccessFactor.Competencies;
using SuccessFactor.Competencies.Assessments;
using SuccessFactor.Competencies.Models;
using SuccessFactor.Cycles;
using SuccessFactor.Employees;
using SuccessFactor.My.Dtos;
using SuccessFactor.My.Support;
using SuccessFactor.Team.Support;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace SuccessFactor.Team;

[Authorize]
public class TeamAssessmentsAppService : ApplicationService, ITeamAssessmentsAppService
{
    private const string FieldScore = "Competencies.Score";
    private const string FieldComment = "Competencies.Comment";
    private const string FieldEvidenceAttachmentId = "Competencies.EvidenceAttachmentId";

    private readonly ITeamWorkflowContextResolver _teamWorkflowContextResolver;
    private readonly IPhasePermissionResolver _phasePermissionResolver;

    private readonly IRepository<Employee, Guid> _employeeRepository;
    private readonly IRepository<CompetencyAssessment, Guid> _assessmentRepository;
    private readonly IRepository<CompetencyAssessmentItem, Guid> _assessmentItemRepository;
    private readonly IRepository<CompetencyModel, Guid> _modelRepository;
    private readonly IRepository<CompetencyModelItem, Guid> _modelItemRepository;
    private readonly IRepository<Competency, Guid> _competencyRepository;
    private readonly CompetencyAssessmentAppService _competencyAssessmentAppService;

    public TeamAssessmentsAppService(
        ITeamWorkflowContextResolver teamWorkflowContextResolver,
        IPhasePermissionResolver phasePermissionResolver,
        IRepository<Employee, Guid> employeeRepository,
        IRepository<CompetencyAssessment, Guid> assessmentRepository,
        IRepository<CompetencyAssessmentItem, Guid> assessmentItemRepository,
        IRepository<CompetencyModel, Guid> modelRepository,
        IRepository<CompetencyModelItem, Guid> modelItemRepository,
        IRepository<Competency, Guid> competencyRepository,
        CompetencyAssessmentAppService competencyAssessmentAppService)
    {
        _teamWorkflowContextResolver = teamWorkflowContextResolver;
        _phasePermissionResolver = phasePermissionResolver;
        _employeeRepository = employeeRepository;
        _assessmentRepository = assessmentRepository;
        _assessmentItemRepository = assessmentItemRepository;
        _modelRepository = modelRepository;
        _modelItemRepository = modelItemRepository;
        _competencyRepository = competencyRepository;
        _competencyAssessmentAppService = competencyAssessmentAppService;
    }

    public async Task<MyAssessmentsDto> GetAsync(GetTeamAssessmentsInput input)
    {
        var context = await _teamWorkflowContextResolver.ResolveAsync(
            input.TargetEmployeeId,
            input.CycleId);

        var phasePermission = await _phasePermissionResolver.GetEffectivePhasePermissionAsync(
            context.Cycle.TemplateId,
            context.TargetParticipant.CurrentPhaseId!.Value,
            context.RoleCodeUsed);

        var fieldAccess = await _phasePermissionResolver.GetEffectiveFieldAccessAsync(
            context.Cycle.TemplateId,
            context.TargetParticipant.CurrentPhaseId.Value,
            context.RoleCodeUsed,
            FieldScore,
            FieldComment,
            FieldEvidenceAttachmentId);

        var employeeQuery = await _employeeRepository.GetQueryableAsync();
        var allEmployees = await AsyncExecuter.ToListAsync(employeeQuery);

        var employeeById = allEmployees
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.First());

        var assessmentQuery = await _assessmentRepository.GetQueryableAsync();

        var assessments = await AsyncExecuter.ToListAsync(
            assessmentQuery
                .Where(x => x.CycleId == context.Cycle.Id && x.EmployeeId == context.TargetEmployee.Id)
                .OrderBy(x => x.AssessmentType));

        if (input.OnlyOpen)
        {
            assessments = assessments
                .Where(x => !string.Equals(x.Status, "Closed", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

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
            var isDraft = string.Equals(assessment.Status, "Draft", StringComparison.OrdinalIgnoreCase);

            employeeById.TryGetValue(assessment.EmployeeId, out var targetEmployee);
            employeeById.TryGetValue(assessment.EvaluatorEmployeeId, out var evaluatorEmployee);

            dtoItems.Add(new MyAssessmentItemDto
            {
                CycleId = context.Cycle.Id,
                EmployeeId = context.TargetEmployee.Id,
                RoleCodeUsed = context.RoleCodeUsed,

                CurrentPhaseId = context.TargetParticipant.CurrentPhaseId,
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
            EmployeeId = context.TargetEmployee.Id,
            EmployeeName = context.TargetEmployee.FullName,
            CycleId = context.Cycle.Id,
            CycleName = context.Cycle.Name,
            CurrentPhaseId = context.TargetParticipant.CurrentPhaseId,
            CurrentPhaseCode = context.CurrentPhase?.Code,
            RoleCodeUsed = context.RoleCodeUsed,
            CanEdit = canEdit,
            CanSubmitAny = dtoItems.Any(x => x.CanSubmit),
            Items = dtoItems
        };
    }
    public async Task SubmitAsync(Guid targetEmployeeId, Guid assessmentId, Guid? cycleId)
    {
        var context = await _teamWorkflowContextResolver.ResolveAsync(
            targetEmployeeId,
            cycleId);

        var assessment = await _assessmentRepository.FirstOrDefaultAsync(x => x.Id == assessmentId);

        if (assessment is null
            || assessment.EmployeeId != context.TargetEmployee.Id
            || assessment.CycleId != context.Cycle.Id)
        {
            throw new Volo.Abp.BusinessException("AssessmentNotFound");
        }

        await _competencyAssessmentAppService.SubmitAsync(assessmentId);
    }
    public async Task UpdateItemAsync(
        Guid targetEmployeeId,
        Guid assessmentId,
        UpdateTeamAssessmentItemDto input,
        Guid? cycleId)
    {
        if (input is null)
        {
            throw new BusinessException("AssessmentItemInputRequired");
        }

        var editableContext = await LoadEditableAssessmentContextAsync(
            targetEmployeeId,
            assessmentId,
            cycleId);

        await ApplyItemChangesAsync(
            assessmentId,
            input,
            editableContext.ScoreAccess,
            editableContext.CommentAccess,
            autoSave: true);
    }

    public async Task<int> UpdateItemsAsync(
        Guid targetEmployeeId,
        Guid assessmentId,
        BulkUpdateTeamAssessmentItemsDto input,
        Guid? cycleId)
    {
        if (input?.Items == null || input.Items.Count == 0)
        {
            throw new BusinessException("AssessmentItemsRequired");
        }

        var editableContext = await LoadEditableAssessmentContextAsync(
            targetEmployeeId,
            assessmentId,
            cycleId);

        var itemsToUpdate = input.Items
            .GroupBy(x => x.CompetencyId)
            .Select(g => g.Last())
            .ToList();

        foreach (var item in itemsToUpdate)
        {
            await ApplyItemChangesAsync(
                assessmentId,
                item,
                editableContext.ScoreAccess,
                editableContext.CommentAccess,
                autoSave: false);
        }

        await CurrentUnitOfWork.SaveChangesAsync();

        return itemsToUpdate.Count;
    }

    private async Task<TeamAssessmentEditContext> LoadEditableAssessmentContextAsync(
        Guid targetEmployeeId,
        Guid assessmentId,
        Guid? cycleId)
    {
        var context = await _teamWorkflowContextResolver.ResolveAsync(
            targetEmployeeId,
            cycleId);

        var assessment = await _assessmentRepository.FirstOrDefaultAsync(x => x.Id == assessmentId);

        if (assessment is null
            || assessment.EmployeeId != context.TargetEmployee.Id
            || assessment.CycleId != context.Cycle.Id)
        {
            throw new BusinessException("AssessmentNotFound");
        }

        var isDraft = string.Equals(assessment.Status, "Draft", StringComparison.OrdinalIgnoreCase);

        var phasePermission = await _phasePermissionResolver.GetEffectivePhasePermissionAsync(
            context.Cycle.TemplateId,
            context.TargetParticipant.CurrentPhaseId!.Value,
            context.RoleCodeUsed);

        if (!(phasePermission?.CanEdit ?? false) || !isDraft)
        {
            throw new BusinessException("AssessmentNotEditable");
        }

        var fieldAccess = await _phasePermissionResolver.GetEffectiveFieldAccessAsync(
            context.Cycle.TemplateId,
            context.TargetParticipant.CurrentPhaseId.Value,
            context.RoleCodeUsed,
            FieldScore,
            FieldComment);

        return new TeamAssessmentEditContext(
            fieldAccess[FieldScore],
            fieldAccess[FieldComment]);
    }

    private async Task ApplyItemChangesAsync(
        Guid assessmentId,
        UpdateTeamAssessmentItemDto input,
        string scoreAccess,
        string commentAccess,
        bool autoSave)
    {
        var item = await _assessmentItemRepository.FirstOrDefaultAsync(x =>
            x.AssessmentId == assessmentId &&
            x.CompetencyId == input.CompetencyId);

        var isNewItem = item == null;

        if (isNewItem)
        {
            item = new CompetencyAssessmentItem
            {
                AssessmentId = assessmentId,
                CompetencyId = input.CompetencyId
            };
        }

        if (string.Equals(scoreAccess, "Edit", StringComparison.OrdinalIgnoreCase))
        {
            item.Score = input.Score;
        }

        if (string.Equals(commentAccess, "Edit", StringComparison.OrdinalIgnoreCase))
        {
            item.Comment = input.Comment;
        }

        if (isNewItem)
        {
            await _assessmentItemRepository.InsertAsync(item, autoSave: autoSave);
            return;
        }

        await _assessmentItemRepository.UpdateAsync(item, autoSave: autoSave);
    }

    private sealed record TeamAssessmentEditContext(
        string ScoreAccess,
        string CommentAccess);
}
