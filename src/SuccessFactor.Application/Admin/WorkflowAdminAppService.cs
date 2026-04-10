using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SuccessFactor.Process;
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
    private readonly IRepository<PhaseRolePermission, Guid> _rolePermissionRepository;
    private readonly IRepository<PhaseFieldPolicy, Guid> _fieldPolicyRepository;

    public WorkflowAdminAppService(
        ICurrentUser currentUser,
        IAsyncQueryableExecuter asyncExecuter,
        IRepository<ProcessTemplate, Guid> templateRepository,
        IRepository<ProcessPhase, Guid> phaseRepository,
        IRepository<PhaseRolePermission, Guid> rolePermissionRepository,
        IRepository<PhaseFieldPolicy, Guid> fieldPolicyRepository)
    {
        _currentUser = currentUser;
        _asyncExecuter = asyncExecuter;
        _templateRepository = templateRepository;
        _phaseRepository = phaseRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _fieldPolicyRepository = fieldPolicyRepository;
    }

    public async Task<WorkflowAdminDto> GetAsync(GetWorkflowAdminInput input)
    {
        EnsureCurrentUserIsAdmin();
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
            Templates = templates.Select(x => new WorkflowTemplateLookupDto
            {
                TemplateId = x.Id,
                TemplateName = x.Name,
                Version = x.Version,
                IsDefault = x.IsDefault
            }).ToList()
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

        var selectedPhase = ResolveSelectedPhase(phases, input.PhaseId);

        dto.Phases = phases.Select(x => new WorkflowPhaseLookupDto
        {
            PhaseId = x.Id,
            PhaseCode = x.Code,
            PhaseName = x.Name,
            PhaseOrder = x.PhaseOrder,
            IsTerminal = x.IsTerminal
        }).ToList();
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

    public async Task<PhaseRolePermissionDto> SaveRolePermissionAsync(Guid? id, CreateUpdatePhaseRolePermissionDto input)
    {
        EnsureCurrentUserIsAdmin();
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

        return MapRolePermission(created);
    }

    public async Task DeleteRolePermissionAsync(Guid id)
    {
        EnsureCurrentUserIsAdmin();
        await _rolePermissionRepository.DeleteAsync(id);
    }

    public async Task<PhaseFieldPolicyDto> SaveFieldPolicyAsync(Guid? id, CreateUpdatePhaseFieldPolicyDto input)
    {
        EnsureCurrentUserIsAdmin();
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

        return MapFieldPolicy(created);
    }

    public async Task DeleteFieldPolicyAsync(Guid id)
    {
        EnsureCurrentUserIsAdmin();
        await _fieldPolicyRepository.DeleteAsync(id);
    }

    private void EnsureCurrentUserIsAdmin()
    {
        var roles = _currentUser.Roles ?? Array.Empty<string>();

        if (!roles.Any(x => x.Contains("admin", StringComparison.OrdinalIgnoreCase)))
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
}
