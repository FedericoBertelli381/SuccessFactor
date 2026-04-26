using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SuccessFactor.Auditing;
using SuccessFactor.Cycles;
using SuccessFactor.Process;
using SuccessFactor.Security;
using SuccessFactor.Workflow;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Users;

namespace SuccessFactor.Admin;

[Authorize]
public class WorkflowAdminAppService : ApplicationService, IWorkflowAdminAppService
{
    private readonly ICurrentUser _currentUser;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IRepository<ProcessTemplate, Guid> _templateRepository;
    private readonly IRepository<ProcessPhase, Guid> _phaseRepository;
    private readonly IRepository<PhaseTransition, Guid> _transitionRepository;
    private readonly IRepository<PhaseRolePermission, Guid> _rolePermissionRepository;
    private readonly IRepository<PhaseFieldPolicy, Guid> _fieldPolicyRepository;
    private readonly IRepository<Cycle, Guid> _cycleRepository;
    private readonly IRepository<CycleParticipant, Guid> _cycleParticipantRepository;
    private readonly IBusinessAuditLogger _auditLogger;

    public WorkflowAdminAppService(
        ICurrentUser currentUser,
        IAsyncQueryableExecuter asyncExecuter,
        IRepository<ProcessTemplate, Guid> templateRepository,
        IRepository<ProcessPhase, Guid> phaseRepository,
        IRepository<PhaseTransition, Guid> transitionRepository,
        IRepository<PhaseRolePermission, Guid> rolePermissionRepository,
        IRepository<PhaseFieldPolicy, Guid> fieldPolicyRepository,
        IRepository<Cycle, Guid> cycleRepository,
        IRepository<CycleParticipant, Guid> cycleParticipantRepository,
        IBusinessAuditLogger auditLogger)
    {
        _currentUser = currentUser;
        _asyncExecuter = asyncExecuter;
        _templateRepository = templateRepository;
        _phaseRepository = phaseRepository;
        _transitionRepository = transitionRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _fieldPolicyRepository = fieldPolicyRepository;
        _cycleRepository = cycleRepository;
        _cycleParticipantRepository = cycleParticipantRepository;
        _auditLogger = auditLogger;
    }

    public async Task<WorkflowAdminDto> GetAsync(GetWorkflowAdminInput input)
    {
        EnsureTenantAndAdmin();
        input ??= new GetWorkflowAdminInput();

        var templateQuery = await _templateRepository.GetQueryableAsync();
        var templates = await _asyncExecuter.ToListAsync(
            templateQuery
                .OrderBy(x => x.Name)
                .ThenByDescending(x => x.Version));

        var selectedTemplate = ResolveSelectedTemplate(templates, input.TemplateId);

        var dto = new WorkflowAdminDto
        {
            SelectedTemplateId = selectedTemplate?.Id,
            SelectedTemplateName = selectedTemplate?.Name,
            SelectedTemplateVersion = selectedTemplate?.Version,
            SelectedTemplateIsDefault = selectedTemplate?.IsDefault ?? false,
            Templates = templates.Select(MapTemplate).ToList()
        };

        if (selectedTemplate is null)
        {
            return dto;
        }

        var phaseQuery = await _phaseRepository.GetQueryableAsync();
        var phases = await _asyncExecuter.ToListAsync(
            phaseQuery
                .Where(x => x.TemplateId == selectedTemplate.Id)
                .OrderBy(x => x.PhaseOrder)
                .ThenBy(x => x.Code));

        dto.Phases = phases.Select(MapPhase).ToList();

        var transitionQuery = await _transitionRepository.GetQueryableAsync();
        var transitions = await _asyncExecuter.ToListAsync(
            transitionQuery
                .Where(x => x.TemplateId == selectedTemplate.Id));

        var phaseById = phases.ToDictionary(x => x.Id);
        dto.Transitions = transitions
            .Select(x => MapTransition(x, phaseById))
            .OrderBy(x => x.FromPhaseCode)
            .ThenBy(x => x.ToPhaseCode)
            .ToList();

        var selectedPhase = ResolveSelectedPhase(phases, input.PhaseId);
        dto.SelectedPhaseId = selectedPhase?.Id;
        dto.SelectedPhaseCode = selectedPhase?.Code;
        dto.SelectedPhaseName = selectedPhase?.Name;

        if (selectedPhase is null)
        {
            return dto;
        }

        var rolePermissionQuery = await _rolePermissionRepository.GetQueryableAsync();
        dto.RolePermissions = (await _asyncExecuter.ToListAsync(
            rolePermissionQuery
                .Where(x => x.TemplateId == selectedTemplate.Id && x.PhaseId == selectedPhase.Id)
                .OrderBy(x => x.RoleCode)))
            .Select(MapRolePermission)
            .ToList();

        var fieldPolicyQuery = await _fieldPolicyRepository.GetQueryableAsync();
        dto.FieldPolicies = (await _asyncExecuter.ToListAsync(
            fieldPolicyQuery
                .Where(x => x.TemplateId == selectedTemplate.Id && x.PhaseId == selectedPhase.Id)
                .OrderBy(x => x.FieldKey)
                .ThenBy(x => x.RoleCode)))
            .Select(MapFieldPolicy)
            .ToList();

        return dto;
    }

    public async Task<WorkflowImportResultDto> ImportAsync(ImportWorkflowSetupInput input)
    {
        EnsureTenantAndAdmin();
        input ??= new ImportWorkflowSetupInput();

        var result = new WorkflowImportResultDto();

        var templateRows = ParseTemplates(input.TemplatesContent, result);
        var phaseRows = ParsePhases(input.PhasesContent, result);
        var transitionRows = ParseTransitions(input.TransitionsContent, result);
        var rolePermissionRows = ParseRolePermissions(input.RolePermissionsContent, result);
        var fieldPolicyRows = ParseFieldPolicies(input.FieldPoliciesContent, result);

        if (!templateRows.Any() &&
            !phaseRows.Any() &&
            !transitionRows.Any() &&
            !rolePermissionRows.Any() &&
            !fieldPolicyRows.Any())
        {
            AddError(result, "WorkflowImport", 0, "-", "Nessun contenuto da importare.");
            return result;
        }

        var importedDefaultTemplates = templateRows.Where(x => x.IsDefault).ToList();
        if (importedDefaultTemplates.Count > 1)
        {
            foreach (var row in importedDefaultTemplates)
            {
                AddError(result, WorkflowImportSections.Templates, row.RowNumber, row.Key, "Solo un template puo essere marcato come default nello stesso import.");
            }
        }

        var templates = await _templateRepository.GetListAsync();
        var phases = await _phaseRepository.GetListAsync();
        var transitions = await _transitionRepository.GetListAsync();
        var rolePermissions = await _rolePermissionRepository.GetListAsync();
        var fieldPolicies = await _fieldPolicyRepository.GetListAsync();
        var templateById = templates.ToDictionary(x => x.Id);
        var phaseById = phases.ToDictionary(x => x.Id);

        var existingTemplateByKey = templates.ToDictionary(
            x => new TemplateKey(x.Name, x.Version),
            x => x);

        var existingPhaseByKey = phases
            .Join(templates,
                phase => phase.TemplateId,
                template => template.Id,
                (phase, template) => new { phase, template })
            .ToDictionary(
                x => new PhaseKey(new TemplateKey(x.template.Name, x.template.Version), x.phase.Code),
                x => x.phase);

        var existingTransitionByKey = transitions
            .Join(templates, transition => transition.TemplateId, template => template.Id, (transition, template) => new { transition, template })
            .ToDictionary(
                x =>
                {
                    var fromPhase = phaseById[x.transition.FromPhaseId];
                    var toPhase = phaseById[x.transition.ToPhaseId];
                    return new TransitionKey(new TemplateKey(x.template.Name, x.template.Version), fromPhase.Code, toPhase.Code);
                },
                x => x.transition);

        var existingRolePermissionByKey = rolePermissions
            .Join(phases, permission => permission.PhaseId, phase => phase.Id, (permission, phase) => new { permission, phase })
            .Join(templates, item => item.permission.TemplateId, template => template.Id, (item, template) => new { item.permission, item.phase, template })
            .ToDictionary(
                x => new RolePermissionKey(new TemplateKey(x.template.Name, x.template.Version), x.phase.Code, x.permission.RoleCode),
                x => x.permission);

        var existingFieldPolicyByKey = fieldPolicies
            .Join(phases, policy => policy.PhaseId, phase => phase.Id, (policy, phase) => new { policy, phase })
            .Join(templates, item => item.policy.TemplateId, template => template.Id, (item, template) => new { item.policy, item.phase, template })
            .ToDictionary(
                x => new FieldPolicyKey(new TemplateKey(x.template.Name, x.template.Version), x.phase.Code, x.policy.FieldKey, x.policy.RoleCode),
                x => x.policy);

        var knownTemplateKeys = new HashSet<TemplateKey>(existingTemplateByKey.Keys);
        var knownPhaseKeys = new HashSet<PhaseKey>(existingPhaseByKey.Keys);
        var seenTemplateRows = new HashSet<TemplateKey>();
        var seenPhaseRows = new HashSet<PhaseKey>();
        var seenTransitionRows = new HashSet<TransitionKey>();
        var seenRolePermissionRows = new HashSet<RolePermissionKey>();
        var seenFieldPolicyRows = new HashSet<FieldPolicyKey>();

        var templateCreates = new List<TemplateImportRow>();
        var phaseCreates = new List<PhaseImportRow>();
        var transitionCreates = new List<TransitionImportRow>();
        var rolePermissionCreates = new List<RolePermissionImportRow>();
        var fieldPolicyCreates = new List<FieldPolicyImportRow>();

        var templateUpdates = new List<(TemplateImportRow Row, ProcessTemplate Entity)>();
        var phaseUpdates = new List<(PhaseImportRow Row, ProcessPhase Entity)>();
        var transitionUpdates = new List<(TransitionImportRow Row, PhaseTransition Entity)>();
        var rolePermissionUpdates = new List<(RolePermissionImportRow Row, PhaseRolePermission Entity)>();
        var fieldPolicyUpdates = new List<(FieldPolicyImportRow Row, PhaseFieldPolicy Entity)>();

        foreach (var row in templateRows)
        {
            if (!seenTemplateRows.Add(row.TemplateKey))
            {
                AddError(result, WorkflowImportSections.Templates, row.RowNumber, row.Key, "Template duplicato nello stesso file.");
                continue;
            }

            if (existingTemplateByKey.TryGetValue(row.TemplateKey, out var existingTemplate))
            {
                if (!input.UpdateExisting)
                {
                    AddError(result, WorkflowImportSections.Templates, row.RowNumber, row.Key, "Template gia esistente. Abilita aggiorna esistenti per modificarlo.");
                    continue;
                }

                templateUpdates.Add((row, existingTemplate));
                AddPreview(result, WorkflowImportSections.Templates, row.RowNumber, row.Key, "Update");
            }
            else
            {
                templateCreates.Add(row);
                knownTemplateKeys.Add(row.TemplateKey);
                AddPreview(result, WorkflowImportSections.Templates, row.RowNumber, row.Key, "Create");
            }
        }

        foreach (var row in phaseRows)
        {
            if (!knownTemplateKeys.Contains(row.TemplateKey))
            {
                AddError(result, WorkflowImportSections.Phases, row.RowNumber, row.Key, "Template non trovato ne nell'import ne tra quelli esistenti.");
                continue;
            }

            if (!seenPhaseRows.Add(row.PhaseKey))
            {
                AddError(result, WorkflowImportSections.Phases, row.RowNumber, row.Key, "Fase duplicata nello stesso file.");
                continue;
            }

            if (existingPhaseByKey.TryGetValue(row.PhaseKey, out var existingPhase))
            {
                if (!input.UpdateExisting)
                {
                    AddError(result, WorkflowImportSections.Phases, row.RowNumber, row.Key, "Fase gia esistente. Abilita aggiorna esistenti per modificarla.");
                    continue;
                }

                phaseUpdates.Add((row, existingPhase));
                AddPreview(result, WorkflowImportSections.Phases, row.RowNumber, row.Key, "Update");
            }
            else
            {
                phaseCreates.Add(row);
                knownPhaseKeys.Add(row.PhaseKey);
                AddPreview(result, WorkflowImportSections.Phases, row.RowNumber, row.Key, "Create");
            }
        }

        foreach (var row in transitionRows)
        {
            if (!knownPhaseKeys.Contains(new PhaseKey(row.TemplateKey, row.FromPhaseCode)))
            {
                AddError(result, WorkflowImportSections.Transitions, row.RowNumber, row.Key, "Fase origine non trovata.");
                continue;
            }

            if (!knownPhaseKeys.Contains(new PhaseKey(row.TemplateKey, row.ToPhaseCode)))
            {
                AddError(result, WorkflowImportSections.Transitions, row.RowNumber, row.Key, "Fase destinazione non trovata.");
                continue;
            }

            if (!seenTransitionRows.Add(row.TransitionKey))
            {
                AddError(result, WorkflowImportSections.Transitions, row.RowNumber, row.Key, "Transizione duplicata nello stesso file.");
                continue;
            }

            if (existingTransitionByKey.TryGetValue(row.TransitionKey, out var existingTransition))
            {
                if (!input.UpdateExisting)
                {
                    AddError(result, WorkflowImportSections.Transitions, row.RowNumber, row.Key, "Transizione gia esistente. Abilita aggiorna esistenti per modificarla.");
                    continue;
                }

                transitionUpdates.Add((row, existingTransition));
                AddPreview(result, WorkflowImportSections.Transitions, row.RowNumber, row.Key, "Update");
            }
            else
            {
                transitionCreates.Add(row);
                AddPreview(result, WorkflowImportSections.Transitions, row.RowNumber, row.Key, "Create");
            }
        }

        foreach (var row in rolePermissionRows)
        {
            if (!knownPhaseKeys.Contains(new PhaseKey(row.TemplateKey, row.PhaseCode)))
            {
                AddError(result, WorkflowImportSections.RolePermissions, row.RowNumber, row.Key, "Fase non trovata.");
                continue;
            }

            if (!seenRolePermissionRows.Add(row.PermissionKey))
            {
                AddError(result, WorkflowImportSections.RolePermissions, row.RowNumber, row.Key, "Role permission duplicata nello stesso file.");
                continue;
            }

            if (existingRolePermissionByKey.TryGetValue(row.PermissionKey, out var existingPermission))
            {
                if (!input.UpdateExisting)
                {
                    AddError(result, WorkflowImportSections.RolePermissions, row.RowNumber, row.Key, "Role permission gia esistente. Abilita aggiorna esistenti per modificarla.");
                    continue;
                }

                rolePermissionUpdates.Add((row, existingPermission));
                AddPreview(result, WorkflowImportSections.RolePermissions, row.RowNumber, row.Key, "Update");
            }
            else
            {
                rolePermissionCreates.Add(row);
                AddPreview(result, WorkflowImportSections.RolePermissions, row.RowNumber, row.Key, "Create");
            }
        }

        foreach (var row in fieldPolicyRows)
        {
            if (!knownPhaseKeys.Contains(new PhaseKey(row.TemplateKey, row.PhaseCode)))
            {
                AddError(result, WorkflowImportSections.FieldPolicies, row.RowNumber, row.Key, "Fase non trovata.");
                continue;
            }

            if (!seenFieldPolicyRows.Add(row.PolicyKey))
            {
                AddError(result, WorkflowImportSections.FieldPolicies, row.RowNumber, row.Key, "Field policy duplicata nello stesso file.");
                continue;
            }

            if (existingFieldPolicyByKey.TryGetValue(row.PolicyKey, out var existingPolicy))
            {
                if (!input.UpdateExisting)
                {
                    AddError(result, WorkflowImportSections.FieldPolicies, row.RowNumber, row.Key, "Field policy gia esistente. Abilita aggiorna esistenti per modificarla.");
                    continue;
                }

                fieldPolicyUpdates.Add((row, existingPolicy));
                AddPreview(result, WorkflowImportSections.FieldPolicies, row.RowNumber, row.Key, "Update");
            }
            else
            {
                fieldPolicyCreates.Add(row);
                AddPreview(result, WorkflowImportSections.FieldPolicies, row.RowNumber, row.Key, "Create");
            }
        }

        if (result.HasErrors)
        {
            result.ErrorCount = result.Rows.Count(x => x.Status == "Error");
            return result;
        }

        var createdTemplatesByKey = new Dictionary<TemplateKey, ProcessTemplate>();
        foreach (var row in templateCreates)
        {
            var entity = await _templateRepository.InsertAsync(new ProcessTemplate
            {
                TenantId = CurrentTenant.Id,
                Name = row.TemplateName,
                Version = row.Version,
                IsDefault = row.IsDefault
            }, autoSave: true);

            createdTemplatesByKey[row.TemplateKey] = entity;
        }

        foreach (var (row, entity) in templateUpdates)
        {
            entity.IsDefault = row.IsDefault;
            await _templateRepository.UpdateAsync(entity, autoSave: true);
        }

        var resolvedTemplatesByKey = new Dictionary<TemplateKey, ProcessTemplate>(existingTemplateByKey);
        foreach (var item in createdTemplatesByKey)
        {
            resolvedTemplatesByKey[item.Key] = item.Value;
        }

        if (templateRows.Any(x => x.IsDefault))
        {
            var selectedDefault = templateRows.Single(x => x.IsDefault).TemplateKey;
            var selectedDefaultTemplate = resolvedTemplatesByKey[selectedDefault];
            foreach (var template in templates.Where(x => x.Id != selectedDefaultTemplate.Id && x.IsDefault))
            {
                template.IsDefault = false;
                await _templateRepository.UpdateAsync(template, autoSave: true);
            }

            foreach (var template in createdTemplatesByKey.Values.Where(x => x.Id != selectedDefaultTemplate.Id))
            {
                if (template.IsDefault)
                {
                    template.IsDefault = false;
                    await _templateRepository.UpdateAsync(template, autoSave: true);
                }
            }
        }

        foreach (var (row, entity) in phaseUpdates)
        {
            entity.Name = row.PhaseName;
            entity.PhaseOrder = row.PhaseOrder;
            entity.IsTerminal = row.IsTerminal;
            entity.StartRule = row.StartRule;
            entity.EndRule = row.EndRule;
        }

        var createdPhasesByKey = new Dictionary<PhaseKey, ProcessPhase>();
        foreach (var row in phaseCreates)
        {
            var template = resolvedTemplatesByKey[row.TemplateKey];
            var entity = await _phaseRepository.InsertAsync(new ProcessPhase
            {
                TemplateId = template.Id,
                Code = row.PhaseCode,
                Name = row.PhaseName,
                PhaseOrder = row.PhaseOrder,
                IsTerminal = row.IsTerminal,
                StartRule = row.StartRule,
                EndRule = row.EndRule
            }, autoSave: true);

            createdPhasesByKey[row.PhaseKey] = entity;
        }

        foreach (var (_, entity) in phaseUpdates)
        {
            await _phaseRepository.UpdateAsync(entity, autoSave: true);
        }

        var resolvedPhasesByKey = new Dictionary<PhaseKey, ProcessPhase>(existingPhaseByKey);
        foreach (var item in createdPhasesByKey)
        {
            resolvedPhasesByKey[item.Key] = item.Value;
        }

        foreach (var (row, entity) in transitionUpdates)
        {
            entity.ConditionExpr = row.ConditionExpr;
        }

        foreach (var row in transitionCreates)
        {
            var template = resolvedTemplatesByKey[row.TemplateKey];
            var fromPhase = resolvedPhasesByKey[new PhaseKey(row.TemplateKey, row.FromPhaseCode)];
            var toPhase = resolvedPhasesByKey[new PhaseKey(row.TemplateKey, row.ToPhaseCode)];
            await _transitionRepository.InsertAsync(new PhaseTransition
            {
                TemplateId = template.Id,
                FromPhaseId = fromPhase.Id,
                ToPhaseId = toPhase.Id,
                ConditionExpr = row.ConditionExpr
            }, autoSave: true);
        }

        foreach (var (_, entity) in transitionUpdates)
        {
            await _transitionRepository.UpdateAsync(entity, autoSave: true);
        }

        foreach (var (row, entity) in rolePermissionUpdates)
        {
            entity.CanView = row.CanView;
            entity.CanEdit = row.CanEdit;
            entity.CanSubmit = row.CanSubmit;
            entity.CanAdvance = row.CanAdvance;
            entity.ConditionExpr = row.ConditionExpr;
        }

        foreach (var row in rolePermissionCreates)
        {
            var template = resolvedTemplatesByKey[row.TemplateKey];
            var phase = resolvedPhasesByKey[new PhaseKey(row.TemplateKey, row.PhaseCode)];
            await _rolePermissionRepository.InsertAsync(new PhaseRolePermission
            {
                TemplateId = template.Id,
                PhaseId = phase.Id,
                RoleCode = row.RoleCode,
                CanView = row.CanView,
                CanEdit = row.CanEdit,
                CanSubmit = row.CanSubmit,
                CanAdvance = row.CanAdvance,
                ConditionExpr = row.ConditionExpr
            }, autoSave: true);
        }

        foreach (var (_, entity) in rolePermissionUpdates)
        {
            await _rolePermissionRepository.UpdateAsync(entity, autoSave: true);
        }

        foreach (var (row, entity) in fieldPolicyUpdates)
        {
            entity.Access = row.Access;
            entity.IsRequired = row.IsRequired;
            entity.ConditionExpr = row.ConditionExpr;
        }

        foreach (var row in fieldPolicyCreates)
        {
            var template = resolvedTemplatesByKey[row.TemplateKey];
            var phase = resolvedPhasesByKey[new PhaseKey(row.TemplateKey, row.PhaseCode)];
            await _fieldPolicyRepository.InsertAsync(new PhaseFieldPolicy
            {
                TemplateId = template.Id,
                PhaseId = phase.Id,
                FieldKey = row.FieldKey,
                RoleCode = row.RoleCode,
                Access = row.Access,
                IsRequired = row.IsRequired,
                ConditionExpr = row.ConditionExpr
            }, autoSave: true);
        }

        foreach (var (_, entity) in fieldPolicyUpdates)
        {
            await _fieldPolicyRepository.UpdateAsync(entity, autoSave: true);
        }

        result.CreatedTemplates = templateCreates.Count;
        result.UpdatedTemplates = templateUpdates.Count;
        result.CreatedPhases = phaseCreates.Count;
        result.UpdatedPhases = phaseUpdates.Count;
        result.CreatedTransitions = transitionCreates.Count;
        result.UpdatedTransitions = transitionUpdates.Count;
        result.CreatedRolePermissions = rolePermissionCreates.Count;
        result.UpdatedRolePermissions = rolePermissionUpdates.Count;
        result.CreatedFieldPolicies = fieldPolicyCreates.Count;
        result.UpdatedFieldPolicies = fieldPolicyUpdates.Count;

        await _auditLogger.LogAsync("WorkflowImportCompleted", "WorkflowImport", "bulk", new Dictionary<string, object?>
        {
            ["CreatedTemplates"] = result.CreatedTemplates,
            ["UpdatedTemplates"] = result.UpdatedTemplates,
            ["CreatedPhases"] = result.CreatedPhases,
            ["UpdatedPhases"] = result.UpdatedPhases,
            ["CreatedTransitions"] = result.CreatedTransitions,
            ["UpdatedTransitions"] = result.UpdatedTransitions,
            ["CreatedRolePermissions"] = result.CreatedRolePermissions,
            ["UpdatedRolePermissions"] = result.UpdatedRolePermissions,
            ["CreatedFieldPolicies"] = result.CreatedFieldPolicies,
            ["UpdatedFieldPolicies"] = result.UpdatedFieldPolicies
        });

        return result;
    }

    public async Task<WorkflowTemplateLookupDto> SaveTemplateAsync(Guid? id, CreateUpdateProcessTemplateDto input)
    {
        EnsureTenantAndAdmin();

        var templateName = NormalizeRequired(input.Name, "TemplateName");
        ValidateVersion(input.Version);

        if (id.HasValue)
        {
            await EnsureNoDuplicateTemplateAsync(templateName, input.Version, id);

            var entity = await _templateRepository.GetAsync(id.Value);
            entity.Name = templateName;
            entity.Version = input.Version;
            entity.IsDefault = input.IsDefault;

            if (input.IsDefault)
            {
                await ClearOtherDefaultsAsync(entity.Id);
            }

            entity = await _templateRepository.UpdateAsync(entity, autoSave: true);

            await _auditLogger.LogAsync("WorkflowTemplateSaved", nameof(ProcessTemplate), entity.Id.ToString(), new Dictionary<string, object?>
            {
                ["ChangeType"] = "Update",
                ["Name"] = entity.Name,
                ["Version"] = entity.Version,
                ["IsDefault"] = entity.IsDefault
            });

            return MapTemplate(entity);
        }

        await EnsureNoDuplicateTemplateAsync(templateName, input.Version, null);

        var created = await _templateRepository.InsertAsync(new ProcessTemplate
        {
            TenantId = CurrentTenant.Id,
            Name = templateName,
            Version = input.Version,
            IsDefault = input.IsDefault
        }, autoSave: true);

        if (input.IsDefault)
        {
            await ClearOtherDefaultsAsync(created.Id);
            created = await _templateRepository.GetAsync(created.Id);
        }

        await _auditLogger.LogAsync("WorkflowTemplateSaved", nameof(ProcessTemplate), created.Id.ToString(), new Dictionary<string, object?>
        {
            ["ChangeType"] = "Create",
            ["Name"] = created.Name,
            ["Version"] = created.Version,
            ["IsDefault"] = created.IsDefault
        });

        return MapTemplate(created);
    }

    public async Task DeleteTemplateAsync(Guid id)
    {
        EnsureTenantAndAdmin();

        var template = await _templateRepository.GetAsync(id);

        if (await _cycleRepository.AnyAsync(x => x.TemplateId == id))
        {
            throw new BusinessException("WorkflowTemplateInUseByCycle");
        }

        if (await _phaseRepository.AnyAsync(x => x.TemplateId == id)
            || await _transitionRepository.AnyAsync(x => x.TemplateId == id)
            || await _rolePermissionRepository.AnyAsync(x => x.TemplateId == id)
            || await _fieldPolicyRepository.AnyAsync(x => x.TemplateId == id))
        {
            throw new BusinessException("WorkflowTemplateHasChildren");
        }

        await _templateRepository.DeleteAsync(id);
        await _auditLogger.LogAsync("WorkflowTemplateDeleted", nameof(ProcessTemplate), id.ToString(), new Dictionary<string, object?>
        {
            ["Name"] = template.Name,
            ["Version"] = template.Version
        });
    }

    public async Task<WorkflowPhaseLookupDto> SavePhaseAsync(Guid? id, CreateUpdateProcessPhaseDto input)
    {
        EnsureTenantAndAdmin();
        await EnsureTemplateExistsAsync(input.TemplateId);

        var phaseCode = NormalizeRequired(input.Code, "PhaseCode");
        var phaseName = NormalizeRequired(input.Name, "PhaseName");

        if (id.HasValue)
        {
            await EnsureNoDuplicatePhaseAsync(input.TemplateId, phaseCode, id);

            var entity = await _phaseRepository.GetAsync(id.Value);
            entity.TemplateId = input.TemplateId;
            entity.Code = phaseCode;
            entity.Name = phaseName;
            entity.PhaseOrder = input.PhaseOrder;
            entity.IsTerminal = input.IsTerminal;
            entity.StartRule = NormalizeNullable(input.StartRule);
            entity.EndRule = NormalizeNullable(input.EndRule);

            entity = await _phaseRepository.UpdateAsync(entity, autoSave: true);

            await _auditLogger.LogAsync("WorkflowPhaseSaved", nameof(ProcessPhase), entity.Id.ToString(), new Dictionary<string, object?>
            {
                ["ChangeType"] = "Update",
                ["TemplateId"] = entity.TemplateId,
                ["Code"] = entity.Code,
                ["Name"] = entity.Name,
                ["PhaseOrder"] = entity.PhaseOrder,
                ["IsTerminal"] = entity.IsTerminal
            });

            return MapPhase(entity);
        }

        await EnsureNoDuplicatePhaseAsync(input.TemplateId, phaseCode, null);

        var created = await _phaseRepository.InsertAsync(new ProcessPhase
        {
            TemplateId = input.TemplateId,
            Code = phaseCode,
            Name = phaseName,
            PhaseOrder = input.PhaseOrder,
            IsTerminal = input.IsTerminal,
            StartRule = NormalizeNullable(input.StartRule),
            EndRule = NormalizeNullable(input.EndRule)
        }, autoSave: true);

        await _auditLogger.LogAsync("WorkflowPhaseSaved", nameof(ProcessPhase), created.Id.ToString(), new Dictionary<string, object?>
        {
            ["ChangeType"] = "Create",
            ["TemplateId"] = created.TemplateId,
            ["Code"] = created.Code,
            ["Name"] = created.Name,
            ["PhaseOrder"] = created.PhaseOrder,
            ["IsTerminal"] = created.IsTerminal
        });

        return MapPhase(created);
    }

    public async Task DeletePhaseAsync(Guid id)
    {
        EnsureTenantAndAdmin();

        var phase = await _phaseRepository.GetAsync(id);

        if (await _cycleRepository.AnyAsync(x => x.CurrentPhaseId == id)
            || await _cycleParticipantRepository.AnyAsync(x => x.CurrentPhaseId == id))
        {
            throw new BusinessException("WorkflowPhaseInUse");
        }

        if (await _transitionRepository.AnyAsync(x => x.FromPhaseId == id || x.ToPhaseId == id)
            || await _rolePermissionRepository.AnyAsync(x => x.PhaseId == id)
            || await _fieldPolicyRepository.AnyAsync(x => x.PhaseId == id))
        {
            throw new BusinessException("WorkflowPhaseHasChildren");
        }

        await _phaseRepository.DeleteAsync(id);
        await _auditLogger.LogAsync("WorkflowPhaseDeleted", nameof(ProcessPhase), id.ToString(), new Dictionary<string, object?>
        {
            ["TemplateId"] = phase.TemplateId,
            ["Code"] = phase.Code,
            ["Name"] = phase.Name
        });
    }

    public async Task<WorkflowTransitionAdminDto> SaveTransitionAsync(Guid? id, CreateUpdatePhaseTransitionDto input)
    {
        EnsureTenantAndAdmin();
        await EnsureTemplateAndTransitionPhasesAsync(input);

        if (input.FromPhaseId == input.ToPhaseId)
        {
            throw new BusinessException("WorkflowTransitionSamePhase");
        }

        if (id.HasValue)
        {
            await EnsureNoDuplicateTransitionAsync(input.TemplateId, input.FromPhaseId, input.ToPhaseId, id);

            var entity = await _transitionRepository.GetAsync(id.Value);
            entity.TemplateId = input.TemplateId;
            entity.FromPhaseId = input.FromPhaseId;
            entity.ToPhaseId = input.ToPhaseId;
            entity.ConditionExpr = NormalizeNullable(input.ConditionExpr);

            entity = await _transitionRepository.UpdateAsync(entity, autoSave: true);

            await _auditLogger.LogAsync("WorkflowTransitionSaved", nameof(PhaseTransition), entity.Id.ToString(), new Dictionary<string, object?>
            {
                ["ChangeType"] = "Update",
                ["TemplateId"] = entity.TemplateId,
                ["FromPhaseId"] = entity.FromPhaseId,
                ["ToPhaseId"] = entity.ToPhaseId
            });

            return await MapTransitionAsync(entity);
        }

        await EnsureNoDuplicateTransitionAsync(input.TemplateId, input.FromPhaseId, input.ToPhaseId, null);

        var created = await _transitionRepository.InsertAsync(new PhaseTransition
        {
            TemplateId = input.TemplateId,
            FromPhaseId = input.FromPhaseId,
            ToPhaseId = input.ToPhaseId,
            ConditionExpr = NormalizeNullable(input.ConditionExpr)
        }, autoSave: true);

        await _auditLogger.LogAsync("WorkflowTransitionSaved", nameof(PhaseTransition), created.Id.ToString(), new Dictionary<string, object?>
        {
            ["ChangeType"] = "Create",
            ["TemplateId"] = created.TemplateId,
            ["FromPhaseId"] = created.FromPhaseId,
            ["ToPhaseId"] = created.ToPhaseId
        });

        return await MapTransitionAsync(created);
    }

    public async Task DeleteTransitionAsync(Guid id)
    {
        EnsureTenantAndAdmin();
        var entity = await _transitionRepository.GetAsync(id);
        await _transitionRepository.DeleteAsync(id);
        await _auditLogger.LogAsync("WorkflowTransitionDeleted", nameof(PhaseTransition), id.ToString(), new Dictionary<string, object?>
        {
            ["TemplateId"] = entity.TemplateId,
            ["FromPhaseId"] = entity.FromPhaseId,
            ["ToPhaseId"] = entity.ToPhaseId
        });
    }

    public async Task<PhaseRolePermissionDto> SaveRolePermissionAsync(Guid? id, CreateUpdatePhaseRolePermissionDto input)
    {
        EnsureTenantAndAdmin();
        await EnsureTemplateAndPhaseAsync(input.TemplateId, input.PhaseId);
        input.RoleCode = NormalizeRoleCode(input.RoleCode);

        if (id.HasValue)
        {
            await EnsureNoDuplicateRolePermissionAsync(input.TemplateId, input.PhaseId, input.RoleCode, id);
            var entity = await _rolePermissionRepository.GetAsync(id.Value);
            entity.TemplateId = input.TemplateId;
            entity.PhaseId = input.PhaseId;
            entity.RoleCode = input.RoleCode;
            entity.CanView = input.CanView;
            entity.CanEdit = input.CanEdit;
            entity.CanSubmit = input.CanSubmit;
            entity.CanAdvance = input.CanAdvance;
            entity.ConditionExpr = NormalizeNullable(input.ConditionExpr);

            entity = await _rolePermissionRepository.UpdateAsync(entity, autoSave: true);
            await LogRolePermissionSavedAsync(entity, "Update");
            return MapRolePermission(entity);
        }

        await EnsureNoDuplicateRolePermissionAsync(input.TemplateId, input.PhaseId, input.RoleCode, null);

        var created = await _rolePermissionRepository.InsertAsync(new PhaseRolePermission
        {
            TemplateId = input.TemplateId,
            PhaseId = input.PhaseId,
            RoleCode = input.RoleCode,
            CanView = input.CanView,
            CanEdit = input.CanEdit,
            CanSubmit = input.CanSubmit,
            CanAdvance = input.CanAdvance,
            ConditionExpr = NormalizeNullable(input.ConditionExpr)
        }, autoSave: true);

        await LogRolePermissionSavedAsync(created, "Create");
        return MapRolePermission(created);
    }

    public async Task DeleteRolePermissionAsync(Guid id)
    {
        EnsureTenantAndAdmin();
        var entity = await _rolePermissionRepository.GetAsync(id);
        await _rolePermissionRepository.DeleteAsync(id);
        await _auditLogger.LogAsync("WorkflowRolePermissionDeleted", nameof(PhaseRolePermission), id.ToString(), new Dictionary<string, object?>
        {
            ["TemplateId"] = entity.TemplateId,
            ["PhaseId"] = entity.PhaseId,
            ["RoleCode"] = entity.RoleCode
        });
    }

    public async Task<PhaseFieldPolicyDto> SaveFieldPolicyAsync(Guid? id, CreateUpdatePhaseFieldPolicyDto input)
    {
        EnsureTenantAndAdmin();
        await EnsureTemplateAndPhaseAsync(input.TemplateId, input.PhaseId);
        input.RoleCode = NormalizeRoleCode(input.RoleCode);
        input.FieldKey = NormalizeRequired(input.FieldKey, "FieldKey");
        input.Access = NormalizeAccess(input.Access);

        if (id.HasValue)
        {
            await EnsureNoDuplicateFieldPolicyAsync(input.TemplateId, input.PhaseId, input.FieldKey, input.RoleCode, id);
            var entity = await _fieldPolicyRepository.GetAsync(id.Value);
            entity.TemplateId = input.TemplateId;
            entity.PhaseId = input.PhaseId;
            entity.FieldKey = input.FieldKey;
            entity.RoleCode = input.RoleCode;
            entity.Access = input.Access;
            entity.IsRequired = input.IsRequired;
            entity.ConditionExpr = NormalizeNullable(input.ConditionExpr);

            entity = await _fieldPolicyRepository.UpdateAsync(entity, autoSave: true);
            await LogFieldPolicySavedAsync(entity, "Update");
            return MapFieldPolicy(entity);
        }

        await EnsureNoDuplicateFieldPolicyAsync(input.TemplateId, input.PhaseId, input.FieldKey, input.RoleCode, null);

        var created = await _fieldPolicyRepository.InsertAsync(new PhaseFieldPolicy
        {
            TemplateId = input.TemplateId,
            PhaseId = input.PhaseId,
            FieldKey = input.FieldKey,
            RoleCode = input.RoleCode,
            Access = input.Access,
            IsRequired = input.IsRequired,
            ConditionExpr = NormalizeNullable(input.ConditionExpr)
        }, autoSave: true);

        await LogFieldPolicySavedAsync(created, "Create");
        return MapFieldPolicy(created);
    }

    public async Task DeleteFieldPolicyAsync(Guid id)
    {
        EnsureTenantAndAdmin();
        var entity = await _fieldPolicyRepository.GetAsync(id);
        await _fieldPolicyRepository.DeleteAsync(id);
        await _auditLogger.LogAsync("WorkflowFieldPolicyDeleted", nameof(PhaseFieldPolicy), id.ToString(), new Dictionary<string, object?>
        {
            ["TemplateId"] = entity.TemplateId,
            ["PhaseId"] = entity.PhaseId,
            ["FieldKey"] = entity.FieldKey,
            ["RoleCode"] = entity.RoleCode
        });
    }

    private Task LogRolePermissionSavedAsync(PhaseRolePermission entity, string changeType)
        => _auditLogger.LogAsync("WorkflowRolePermissionSaved", nameof(PhaseRolePermission), entity.Id.ToString(), new Dictionary<string, object?>
        {
            ["ChangeType"] = changeType,
            ["TemplateId"] = entity.TemplateId,
            ["PhaseId"] = entity.PhaseId,
            ["RoleCode"] = entity.RoleCode,
            ["CanView"] = entity.CanView,
            ["CanEdit"] = entity.CanEdit,
            ["CanSubmit"] = entity.CanSubmit,
            ["CanAdvance"] = entity.CanAdvance
        });

    private Task LogFieldPolicySavedAsync(PhaseFieldPolicy entity, string changeType)
        => _auditLogger.LogAsync("WorkflowFieldPolicySaved", nameof(PhaseFieldPolicy), entity.Id.ToString(), new Dictionary<string, object?>
        {
            ["ChangeType"] = changeType,
            ["TemplateId"] = entity.TemplateId,
            ["PhaseId"] = entity.PhaseId,
            ["FieldKey"] = entity.FieldKey,
            ["RoleCode"] = entity.RoleCode,
            ["Access"] = entity.Access,
            ["IsRequired"] = entity.IsRequired
        });

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

    private async Task EnsureTemplateAndPhaseAsync(Guid templateId, Guid phaseId)
    {
        if (templateId == Guid.Empty)
        {
            throw new BusinessException("TemplateIdRequired");
        }

        if (phaseId == Guid.Empty)
        {
            throw new BusinessException("PhaseIdRequired");
        }

        if (!await _templateRepository.AnyAsync(x => x.Id == templateId))
        {
            throw new BusinessException("WorkflowAdminTemplateNotFound");
        }

        if (!await _phaseRepository.AnyAsync(x => x.Id == phaseId && x.TemplateId == templateId))
        {
            throw new BusinessException("WorkflowAdminPhaseNotFound");
        }
    }

    private async Task EnsureTemplateExistsAsync(Guid templateId)
    {
        if (templateId == Guid.Empty)
        {
            throw new BusinessException("TemplateIdRequired");
        }

        if (!await _templateRepository.AnyAsync(x => x.Id == templateId))
        {
            throw new BusinessException("WorkflowAdminTemplateNotFound");
        }
    }

    private async Task EnsureTemplateAndTransitionPhasesAsync(CreateUpdatePhaseTransitionDto input)
    {
        await EnsureTemplateExistsAsync(input.TemplateId);

        if (!await _phaseRepository.AnyAsync(x => x.Id == input.FromPhaseId && x.TemplateId == input.TemplateId))
        {
            throw new BusinessException("FromPhaseNotInTemplate");
        }

        if (!await _phaseRepository.AnyAsync(x => x.Id == input.ToPhaseId && x.TemplateId == input.TemplateId))
        {
            throw new BusinessException("ToPhaseNotInTemplate");
        }
    }

    private async Task EnsureNoDuplicateTemplateAsync(string name, int version, Guid? excludeId)
    {
        if (await _templateRepository.AnyAsync(x =>
            x.Name == name &&
            x.Version == version &&
            (!excludeId.HasValue || x.Id != excludeId.Value)))
        {
            throw new BusinessException("WorkflowTemplateAlreadyExists");
        }
    }

    private async Task EnsureNoDuplicatePhaseAsync(Guid templateId, string code, Guid? excludeId)
    {
        if (await _phaseRepository.AnyAsync(x =>
            x.TemplateId == templateId &&
            x.Code == code &&
            (!excludeId.HasValue || x.Id != excludeId.Value)))
        {
            throw new BusinessException("WorkflowPhaseAlreadyExists");
        }
    }

    private async Task EnsureNoDuplicateTransitionAsync(Guid templateId, Guid fromPhaseId, Guid toPhaseId, Guid? excludeId)
    {
        if (await _transitionRepository.AnyAsync(x =>
            x.TemplateId == templateId &&
            x.FromPhaseId == fromPhaseId &&
            x.ToPhaseId == toPhaseId &&
            (!excludeId.HasValue || x.Id != excludeId.Value)))
        {
            throw new BusinessException("WorkflowTransitionAlreadyExists");
        }
    }

    private async Task EnsureNoDuplicateRolePermissionAsync(Guid templateId, Guid phaseId, string roleCode, Guid? excludeId)
    {
        if (await _rolePermissionRepository.AnyAsync(x =>
            x.TemplateId == templateId &&
            x.PhaseId == phaseId &&
            x.RoleCode == roleCode &&
            (!excludeId.HasValue || x.Id != excludeId.Value)))
        {
            throw new BusinessException("PhaseRolePermissionAlreadyExists");
        }
    }

    private async Task EnsureNoDuplicateFieldPolicyAsync(Guid templateId, Guid phaseId, string fieldKey, string roleCode, Guid? excludeId)
    {
        if (await _fieldPolicyRepository.AnyAsync(x =>
            x.TemplateId == templateId &&
            x.PhaseId == phaseId &&
            x.FieldKey == fieldKey &&
            x.RoleCode == roleCode &&
            (!excludeId.HasValue || x.Id != excludeId.Value)))
        {
            throw new BusinessException("PhaseFieldPolicyAlreadyExists");
        }
    }

    private async Task ClearOtherDefaultsAsync(Guid excludeId)
    {
        var defaults = await _templateRepository.GetListAsync(x => x.IsDefault && x.Id != excludeId);

        foreach (var item in defaults)
        {
            item.IsDefault = false;
            await _templateRepository.UpdateAsync(item, autoSave: true);
        }
    }

    private static ProcessTemplate? ResolveSelectedTemplate(List<ProcessTemplate> templates, Guid? templateId)
    {
        if (templates.Count == 0)
        {
            return null;
        }

        if (templateId.HasValue)
        {
            var selected = templates.FirstOrDefault(x => x.Id == templateId.Value);
            if (selected is null)
            {
                throw new BusinessException("WorkflowAdminTemplateNotFound");
            }

            return selected;
        }

        return templates
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Name)
            .First();
    }

    private static ProcessPhase? ResolveSelectedPhase(List<ProcessPhase> phases, Guid? phaseId)
    {
        if (phases.Count == 0)
        {
            return null;
        }

        if (phaseId.HasValue)
        {
            var selected = phases.FirstOrDefault(x => x.Id == phaseId.Value);
            if (selected is null)
            {
                throw new BusinessException("WorkflowAdminPhaseNotFound");
            }

            return selected;
        }

        return phases
            .OrderBy(x => x.PhaseOrder)
            .ThenBy(x => x.Code)
            .First();
    }

    private static WorkflowTemplateLookupDto MapTemplate(ProcessTemplate source)
    {
        return new WorkflowTemplateLookupDto
        {
            TemplateId = source.Id,
            TemplateName = source.Name,
            Version = source.Version,
            IsDefault = source.IsDefault
        };
    }

    private static WorkflowPhaseLookupDto MapPhase(ProcessPhase source)
    {
        return new WorkflowPhaseLookupDto
        {
            PhaseId = source.Id,
            TemplateId = source.TemplateId,
            PhaseCode = source.Code,
            PhaseName = source.Name,
            PhaseOrder = source.PhaseOrder,
            IsTerminal = source.IsTerminal
        };
    }

    private static WorkflowTransitionAdminDto MapTransition(PhaseTransition source, IReadOnlyDictionary<Guid, ProcessPhase> phaseById)
    {
        phaseById.TryGetValue(source.FromPhaseId, out var fromPhase);
        phaseById.TryGetValue(source.ToPhaseId, out var toPhase);

        return new WorkflowTransitionAdminDto
        {
            Id = source.Id,
            TemplateId = source.TemplateId,
            FromPhaseId = source.FromPhaseId,
            FromPhaseCode = fromPhase?.Code ?? "-",
            FromPhaseName = fromPhase?.Name ?? "-",
            ToPhaseId = source.ToPhaseId,
            ToPhaseCode = toPhase?.Code ?? "-",
            ToPhaseName = toPhase?.Name ?? "-",
            ConditionExpr = source.ConditionExpr
        };
    }

    private async Task<WorkflowTransitionAdminDto> MapTransitionAsync(PhaseTransition source)
    {
        var phases = await _phaseRepository.GetListAsync(x => x.Id == source.FromPhaseId || x.Id == source.ToPhaseId);
        return MapTransition(source, phases.ToDictionary(x => x.Id));
    }

    private static PhaseRolePermissionDto MapRolePermission(PhaseRolePermission source)
    {
        return new PhaseRolePermissionDto
        {
            Id = source.Id,
            TemplateId = source.TemplateId,
            PhaseId = source.PhaseId,
            RoleCode = source.RoleCode,
            CanView = source.CanView,
            CanEdit = source.CanEdit,
            CanSubmit = source.CanSubmit,
            CanAdvance = source.CanAdvance,
            ConditionExpr = source.ConditionExpr
        };
    }

    private static PhaseFieldPolicyDto MapFieldPolicy(PhaseFieldPolicy source)
    {
        return new PhaseFieldPolicyDto
        {
            Id = source.Id,
            TemplateId = source.TemplateId,
            PhaseId = source.PhaseId,
            FieldKey = source.FieldKey,
            RoleCode = source.RoleCode,
            Access = source.Access,
            IsRequired = source.IsRequired,
            ConditionExpr = source.ConditionExpr
        };
    }

    private static string NormalizeRoleCode(string? value)
        => NormalizeRequired(value, "RoleCode");

    private static string NormalizeAccess(string? value)
    {
        var normalized = NormalizeRequired(value, "Access");

        if (normalized is not ("Hidden" or "Read" or "Edit"))
        {
            throw new BusinessException("InvalidFieldAccess");
        }

        return normalized;
    }

    private static string NormalizeRequired(string? value, string fieldName)
    {
        var normalized = NormalizeNullable(value);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new BusinessException($"{fieldName}Required");
        }

        return normalized;
    }

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidateVersion(int version)
    {
        if (version <= 0)
        {
            throw new BusinessException("TemplateVersionInvalid");
        }
    }

    private static List<TemplateImportRow> ParseTemplates(string? content, WorkflowImportResultDto result)
        => ParseContent(content, WorkflowImportSections.Templates, 3, result, ParseTemplateRow);

    private static List<PhaseImportRow> ParsePhases(string? content, WorkflowImportResultDto result)
        => ParseContent(content, WorkflowImportSections.Phases, 8, result, ParsePhaseRow);

    private static List<TransitionImportRow> ParseTransitions(string? content, WorkflowImportResultDto result)
        => ParseContent(content, WorkflowImportSections.Transitions, 5, result, ParseTransitionRow);

    private static List<RolePermissionImportRow> ParseRolePermissions(string? content, WorkflowImportResultDto result)
        => ParseContent(content, WorkflowImportSections.RolePermissions, 9, result, ParseRolePermissionRow);

    private static List<FieldPolicyImportRow> ParseFieldPolicies(string? content, WorkflowImportResultDto result)
        => ParseContent(content, WorkflowImportSections.FieldPolicies, 8, result, ParseFieldPolicyRow);

    private static List<T> ParseContent<T>(
        string? content,
        string section,
        int expectedColumns,
        WorkflowImportResultDto result,
        Func<string[], int, T> parser)
        where T : ImportRowBase
    {
        var rows = new List<T>();
        if (string.IsNullOrWhiteSpace(content))
        {
            return rows;
        }

        var lines = content.Replace("\r", string.Empty).Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var rowNumber = index + 1;
            try
            {
                var columns = line.Split(';');
                if (columns.Length != expectedColumns)
                {
                    throw new BusinessException($"Formato non valido: attese {expectedColumns} colonne separate da ';'.");
                }

                rows.Add(parser(columns, rowNumber));
            }
            catch (Exception ex)
            {
                AddError(result, section, rowNumber, line, UiErrorMessage(ex));
            }
        }

        return rows;
    }

    private static TemplateImportRow ParseTemplateRow(string[] columns, int rowNumber)
    {
        var templateName = NormalizeRequired(columns[0], "TemplateName");
        var version = ParsePositiveInt(columns[1], "Version");
        var isDefault = ParseBoolean(columns[2], "IsDefault");
        return new TemplateImportRow(rowNumber, templateName, version, isDefault);
    }

    private static PhaseImportRow ParsePhaseRow(string[] columns, int rowNumber)
    {
        var templateName = NormalizeRequired(columns[0], "TemplateName");
        var version = ParsePositiveInt(columns[1], "Version");
        var phaseCode = NormalizeRequired(columns[2], "PhaseCode");
        var phaseName = NormalizeRequired(columns[3], "PhaseName");
        var phaseOrder = ParsePositiveInt(columns[4], "PhaseOrder");
        var isTerminal = ParseBoolean(columns[5], "IsTerminal");
        var startRule = NormalizeNullable(columns[6]);
        var endRule = NormalizeNullable(columns[7]);
        return new PhaseImportRow(rowNumber, templateName, version, phaseCode, phaseName, phaseOrder, isTerminal, startRule, endRule);
    }

    private static TransitionImportRow ParseTransitionRow(string[] columns, int rowNumber)
    {
        var templateName = NormalizeRequired(columns[0], "TemplateName");
        var version = ParsePositiveInt(columns[1], "Version");
        var fromPhaseCode = NormalizeRequired(columns[2], "FromPhaseCode");
        var toPhaseCode = NormalizeRequired(columns[3], "ToPhaseCode");
        if (string.Equals(fromPhaseCode, toPhaseCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("WorkflowTransitionSamePhase");
        }

        var conditionExpr = NormalizeNullable(columns[4]);
        return new TransitionImportRow(rowNumber, templateName, version, fromPhaseCode, toPhaseCode, conditionExpr);
    }

    private static RolePermissionImportRow ParseRolePermissionRow(string[] columns, int rowNumber)
    {
        var templateName = NormalizeRequired(columns[0], "TemplateName");
        var version = ParsePositiveInt(columns[1], "Version");
        var phaseCode = NormalizeRequired(columns[2], "PhaseCode");
        var roleCode = NormalizeRoleCode(columns[3]);
        var canView = ParseBoolean(columns[4], "CanView");
        var canEdit = ParseBoolean(columns[5], "CanEdit");
        var canSubmit = ParseBoolean(columns[6], "CanSubmit");
        var canAdvance = ParseBoolean(columns[7], "CanAdvance");
        var conditionExpr = NormalizeNullable(columns[8]);
        return new RolePermissionImportRow(rowNumber, templateName, version, phaseCode, roleCode, canView, canEdit, canSubmit, canAdvance, conditionExpr);
    }

    private static FieldPolicyImportRow ParseFieldPolicyRow(string[] columns, int rowNumber)
    {
        var templateName = NormalizeRequired(columns[0], "TemplateName");
        var version = ParsePositiveInt(columns[1], "Version");
        var phaseCode = NormalizeRequired(columns[2], "PhaseCode");
        var fieldKey = NormalizeRequired(columns[3], "FieldKey");
        var roleCode = NormalizeRoleCode(columns[4]);
        var access = NormalizeAccess(columns[5]);
        var isRequired = ParseBoolean(columns[6], "IsRequired");
        var conditionExpr = NormalizeNullable(columns[7]);
        return new FieldPolicyImportRow(rowNumber, templateName, version, phaseCode, fieldKey, roleCode, access, isRequired, conditionExpr);
    }

    private static int ParsePositiveInt(string? value, string fieldName)
    {
        if (!int.TryParse(NormalizeRequired(value, fieldName), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
        {
            throw new BusinessException($"{fieldName}Invalid");
        }

        return parsed;
    }

    private static bool ParseBoolean(string? value, string fieldName)
    {
        var normalized = NormalizeRequired(value, fieldName).ToLowerInvariant();
        return normalized switch
        {
            "true" or "1" or "yes" or "y" or "si" or "s" => true,
            "false" or "0" or "no" or "n" => false,
            _ => throw new BusinessException($"{fieldName}Invalid")
        };
    }

    private static ProcessTemplate? ResolveTemplate(
        TemplateKey key,
        IReadOnlyDictionary<TemplateKey, ProcessTemplate> staged,
        IReadOnlyDictionary<TemplateKey, ProcessTemplate> existing)
        => staged.TryGetValue(key, out var stagedEntity)
            ? stagedEntity
            : existing.GetValueOrDefault(key);

    private static ProcessPhase? ResolvePhase(
        PhaseKey key,
        IReadOnlyDictionary<PhaseKey, ProcessPhase> staged,
        IReadOnlyDictionary<PhaseKey, ProcessPhase> existing)
        => staged.TryGetValue(key, out var stagedEntity)
            ? stagedEntity
            : existing.GetValueOrDefault(key);

    private static void AddPreview(WorkflowImportResultDto result, string section, int rowNumber, string key, string status)
        => result.Rows.Add(new WorkflowImportRowResultDto
        {
            Section = section,
            RowNumber = rowNumber,
            Key = key,
            Status = status
        });

    private static void AddError(WorkflowImportResultDto result, string section, int rowNumber, string key, string message)
    {
        result.HasErrors = true;
        result.Rows.Add(new WorkflowImportRowResultDto
        {
            Section = section,
            RowNumber = rowNumber,
            Key = key,
            Status = "Error",
            Message = message
        });
    }

    private static string UiErrorMessage(Exception ex)
        => ex is BusinessException businessException
            ? businessException.Code ?? businessException.Message
            : ex.Message;

    private static class WorkflowImportSections
    {
        public const string Templates = "Templates";
        public const string Phases = "Phases";
        public const string Transitions = "Transitions";
        public const string RolePermissions = "RolePermissions";
        public const string FieldPolicies = "FieldPolicies";
    }

    private abstract record ImportRowBase(int RowNumber, string Key);

    private sealed record TemplateImportRow(int RowNumber, string TemplateName, int Version, bool IsDefault)
        : ImportRowBase(RowNumber, $"{TemplateName}|v{Version}")
    {
        public TemplateKey TemplateKey => new(TemplateName, Version);
    }

    private sealed record PhaseImportRow(
        int RowNumber,
        string TemplateName,
        int Version,
        string PhaseCode,
        string PhaseName,
        int PhaseOrder,
        bool IsTerminal,
        string? StartRule,
        string? EndRule)
        : ImportRowBase(RowNumber, $"{TemplateName}|v{Version}|{PhaseCode}")
    {
        public TemplateKey TemplateKey => new(TemplateName, Version);
        public PhaseKey PhaseKey => new(TemplateKey, PhaseCode);
    }

    private sealed record TransitionImportRow(
        int RowNumber,
        string TemplateName,
        int Version,
        string FromPhaseCode,
        string ToPhaseCode,
        string? ConditionExpr)
        : ImportRowBase(RowNumber, $"{TemplateName}|v{Version}|{FromPhaseCode}->{ToPhaseCode}")
    {
        public TemplateKey TemplateKey => new(TemplateName, Version);
        public TransitionKey TransitionKey => new(TemplateKey, FromPhaseCode, ToPhaseCode);
    }

    private sealed record RolePermissionImportRow(
        int RowNumber,
        string TemplateName,
        int Version,
        string PhaseCode,
        string RoleCode,
        bool CanView,
        bool CanEdit,
        bool CanSubmit,
        bool CanAdvance,
        string? ConditionExpr)
        : ImportRowBase(RowNumber, $"{TemplateName}|v{Version}|{PhaseCode}|{RoleCode}")
    {
        public TemplateKey TemplateKey => new(TemplateName, Version);
        public RolePermissionKey PermissionKey => new(TemplateKey, PhaseCode, RoleCode);
    }

    private sealed record FieldPolicyImportRow(
        int RowNumber,
        string TemplateName,
        int Version,
        string PhaseCode,
        string FieldKey,
        string RoleCode,
        string Access,
        bool IsRequired,
        string? ConditionExpr)
        : ImportRowBase(RowNumber, $"{TemplateName}|v{Version}|{PhaseCode}|{FieldKey}|{RoleCode}")
    {
        public TemplateKey TemplateKey => new(TemplateName, Version);
        public FieldPolicyKey PolicyKey => new(TemplateKey, PhaseCode, FieldKey, RoleCode);
    }

    private sealed record TemplateKey(string TemplateName, int Version);
    private sealed record PhaseKey(TemplateKey TemplateKey, string PhaseCode);
    private sealed record TransitionKey(TemplateKey TemplateKey, string FromPhaseCode, string ToPhaseCode);
    private sealed record RolePermissionKey(TemplateKey TemplateKey, string PhaseCode, string RoleCode);
    private sealed record FieldPolicyKey(TemplateKey TemplateKey, string PhaseCode, string FieldKey, string RoleCode);
}
