using SuccessFactor.Auditing;
using SuccessFactor.Competencies.Models;
using SuccessFactor.Cycles;
using SuccessFactor.Employees;
using SuccessFactor.Workflow;
using SuccessFactor.Workflow.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
namespace SuccessFactor.Competencies.Assessments;

public class CompetencyAssessmentAppService : ApplicationService
{
    private readonly IRepository<CompetencyAssessment, Guid> _assessmentRepository;
    private readonly IRepository<CompetencyAssessmentItem, Guid> _assessmentItemRepository;

    private readonly IRepository<CompetencyModel, Guid> _modelRepository;
    private readonly IRepository<CompetencyModelItem, Guid> _modelItemRepository;

    private readonly IRepository<Cycle, Guid> _cycleRepository;
    private readonly IRepository<Employee, Guid> _employeeRepository;
    private readonly WorkflowAuthorizationService _workflowAuthorizationService;
    private readonly IBusinessAuditLogger _auditLogger;
    public CompetencyAssessmentAppService(
        IRepository<CompetencyAssessment, Guid> assessmentRepository,
        IRepository<CompetencyAssessmentItem, Guid> assessmentItemRepository,
        IRepository<CompetencyModel, Guid> modelRepository,
        IRepository<CompetencyModelItem, Guid> modelItemRepository,
        IRepository<Cycle, Guid> cycleRepository,
        IRepository<Employee, Guid> employeeRepository,
        WorkflowAuthorizationService workflowAuthorizationService,
        IBusinessAuditLogger auditLogger)
    {
        _assessmentRepository = assessmentRepository;
        _assessmentItemRepository = assessmentItemRepository;
        _modelRepository = modelRepository;
        _modelItemRepository = modelItemRepository;
        _cycleRepository = cycleRepository;
        _employeeRepository = employeeRepository;
        _workflowAuthorizationService = workflowAuthorizationService;
        _auditLogger = auditLogger;
    }

    public async Task<Guid> CreateFromModelAsync(CreateAssessmentFromModelDto input)
    {
        EnsureTenant();

        if (!await _cycleRepository.AnyAsync(c => c.Id == input.CycleId))
            throw new BusinessException("CycleNotFound");

        if (!await _employeeRepository.AnyAsync(e => e.Id == input.EmployeeId))
            throw new BusinessException("EmployeeNotFound");

        if (!await _employeeRepository.AnyAsync(e => e.Id == input.EvaluatorEmployeeId))
            throw new BusinessException("EvaluatorNotFound");

        var model = await _modelRepository.FirstOrDefaultAsync(m => m.Id == input.ModelId);
        if (model == null) throw new BusinessException("CompetencyModelNotFound");

        var assessment = new CompetencyAssessment
        {
            TenantId = CurrentTenant.Id,
            CycleId = input.CycleId,
            EmployeeId = input.EmployeeId,
            EvaluatorEmployeeId = input.EvaluatorEmployeeId,
            ModelId = input.ModelId,
            AssessmentType = input.AssessmentType,
            Status = "Draft"
        };

        await _assessmentRepository.InsertAsync(assessment, autoSave: true);

        // crea items dal modello
        var modelItems = await _modelItemRepository.GetListAsync(mi => mi.ModelId == input.ModelId);
        foreach (var mi in modelItems)
        {
            var item = new CompetencyAssessmentItem
            {
                TenantId = CurrentTenant.Id,
                AssessmentId = assessment.Id,
                CompetencyId = mi.CompetencyId
            };
            await _assessmentItemRepository.InsertAsync(item, autoSave: true);
        }

        return assessment.Id;
    }

    public async Task UpsertItemAsync(UpsertAssessmentItemDto input)
    {
        EnsureTenant();

        var assessment = await _assessmentRepository.FirstOrDefaultAsync(a => a.Id == input.AssessmentId);
        if (assessment == null)
            throw new BusinessException("AssessmentNotFound");

        if (string.Equals(assessment.Status, "Closed", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(SuccessFactorDomainErrorCodes.AssessmentClosed);
        }

        if (!string.Equals(assessment.Status, "Draft", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(SuccessFactorDomainErrorCodes.PhaseEditNotAllowed);
        }

        if (!assessment.ModelId.HasValue)
            throw new BusinessException("AssessmentMissingModel");

        var auth = await _workflowAuthorizationService.EvaluateAsync(assessment.CycleId, assessment.EmployeeId);
        auth.EnsureCanEdit();

        if (input.Score.HasValue)
            auth.EnsureFieldEditable(FieldKeys.Comp_Score);

        if (input.Comment != null)
            auth.EnsureFieldEditable(FieldKeys.Comp_Comment);

        if (input.EvidenceAttachmentId.HasValue)
            auth.EnsureFieldEditable(FieldKeys.Comp_EvidenceAttachment);

        var model = await _modelRepository.GetAsync(assessment.ModelId.Value);

        if (input.Score.HasValue && (input.Score.Value < model.MinScore || input.Score.Value > model.MaxScore))
        {
            throw new BusinessException("ScoreOutOfRange")
                .WithData("Min", model.MinScore)
                .WithData("Max", model.MaxScore);
        }

        var item = await _assessmentItemRepository.FirstOrDefaultAsync(i =>
            i.AssessmentId == input.AssessmentId && i.CompetencyId == input.CompetencyId);

        if (item == null)
            throw new BusinessException("AssessmentItemNotFound");

        item.Score = input.Score;
        item.Comment = input.Comment;
        item.EvidenceAttachmentId = input.EvidenceAttachmentId;

        await _assessmentItemRepository.UpdateAsync(item, autoSave: true);
        await _auditLogger.LogAsync("AssessmentItemUpdated", nameof(CompetencyAssessmentItem), item.Id.ToString(), new Dictionary<string, object?>
        {
            ["AssessmentId"] = assessment.Id,
            ["CycleId"] = assessment.CycleId,
            ["EmployeeId"] = assessment.EmployeeId,
            ["EvaluatorEmployeeId"] = assessment.EvaluatorEmployeeId,
            ["CompetencyId"] = item.CompetencyId,
            ["HasScore"] = item.Score.HasValue,
            ["HasComment"] = !string.IsNullOrWhiteSpace(item.Comment),
            ["HasEvidenceAttachment"] = item.EvidenceAttachmentId.HasValue
        });
    }

    public async Task SubmitAsync(Guid assessmentId)
    {
        EnsureTenant();

        var assessment = await _assessmentRepository.GetAsync(assessmentId);

        if (string.Equals(assessment.Status, "Closed", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(SuccessFactorDomainErrorCodes.AssessmentClosed);
        }

        // idempotenza semplice: se è già Submitted non rilanciamo errore
        if (string.Equals(assessment.Status, "Submitted", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var access = await _workflowAuthorizationService.EvaluateAsync(
            assessment.CycleId,
            assessment.EmployeeId);

        // nel glossario non c'è ancora un PhaseSubmitNotAllowed:
        // riusiamo PhaseEditNotAllowed per non allargare adesso il perimetro error codes
        if (!access.CanSubmit)
        {
            throw new BusinessException(SuccessFactorDomainErrorCodes.PhaseEditNotAllowed);
        }

        var hasMissingRequiredScores = await HasMissingRequiredScoresAsync(
            assessmentId,
            assessment.ModelId);

        if (hasMissingRequiredScores)
        {
            throw new BusinessException(SuccessFactorDomainErrorCodes.RequiredCompetenciesMissingScore);
        }

        assessment.Status = "Submitted";

        await _assessmentRepository.UpdateAsync(assessment, autoSave: true);
        await _auditLogger.LogAsync("AssessmentSubmitted", nameof(CompetencyAssessment), assessment.Id.ToString(), new Dictionary<string, object?>
        {
            ["CycleId"] = assessment.CycleId,
            ["EmployeeId"] = assessment.EmployeeId,
            ["EvaluatorEmployeeId"] = assessment.EvaluatorEmployeeId,
            ["AssessmentType"] = assessment.AssessmentType
        });
    }
    private async Task<bool> HasMissingRequiredScoresAsync(Guid assessmentId, Guid? modelId)
    {
        if (!modelId.HasValue)
        {
            return false;
        }

        var requiredModelItems = await _modelItemRepository.GetListAsync(
            x => x.ModelId == modelId.Value && x.IsRequired);

        if (requiredModelItems.Count == 0)
        {
            return false;
        }

        var assessmentItems = await _assessmentItemRepository.GetListAsync(
            x => x.AssessmentId == assessmentId);

        var assessmentItemsByCompetencyId = assessmentItems
            .GroupBy(x => x.CompetencyId)
            .ToDictionary(x => x.Key, x => x.First());

        foreach (var requiredModelItem in requiredModelItems)
        {
            if (!assessmentItemsByCompetencyId.TryGetValue(requiredModelItem.CompetencyId, out var assessmentItem))
            {
                return true;
            }

            if (!assessmentItem.Score.HasValue)
            {
                return true;
            }
        }

        return false;
    }

    public async Task CloseAsync(Guid assessmentId)
    {
        EnsureTenant();

        var assessment = await _assessmentRepository.GetAsync(assessmentId);
        if (!string.Equals(assessment.Status, "Submitted", StringComparison.OrdinalIgnoreCase))
            throw new BusinessException("AssessmentNotSubmitted");

        assessment.Status = "Closed";
        await _assessmentRepository.UpdateAsync(assessment, autoSave: true);
    }

    public async Task<CompetencyAssessmentDetailsDto> GetDetailsAsync(Guid assessmentId)
    {
        EnsureTenant();

        var assessment = await _assessmentRepository.GetAsync(assessmentId);
        if (!assessment.ModelId.HasValue) throw new BusinessException("AssessmentMissingModel");

        var model = await _modelRepository.GetAsync(assessment.ModelId.Value);

        var items = await _assessmentItemRepository.GetListAsync(i => i.AssessmentId == assessmentId);
        var dto = new CompetencyAssessmentDetailsDto
        {
            AssessmentId = assessment.Id,
            CycleId = assessment.CycleId,
            EmployeeId = assessment.EmployeeId,
            EvaluatorEmployeeId = assessment.EvaluatorEmployeeId,
            ModelId = assessment.ModelId.Value,
            AssessmentType = assessment.AssessmentType,
            Status = assessment.Status,
            MinScore = model.MinScore,
            MaxScore = model.MaxScore
        };

        dto.Items = items.Select(i => new CompetencyAssessmentItemDto
        {
            CompetencyId = i.CompetencyId,
            Score = i.Score,
            Comment = i.Comment,
            EvidenceAttachmentId = i.EvidenceAttachmentId
        }).ToList();

        return dto;
    }

    private void EnsureTenant()
    {
        if (CurrentTenant.Id == null) throw new BusinessException("TenantMissing");
    }
}