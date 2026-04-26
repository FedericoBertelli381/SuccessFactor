using System;
using System.Collections.Generic;
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
}
