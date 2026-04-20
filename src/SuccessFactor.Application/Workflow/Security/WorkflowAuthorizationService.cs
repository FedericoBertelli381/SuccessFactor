using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Users;
using SuccessFactor.Cycles;
using SuccessFactor.Employees;
using SuccessFactor.Security;
using SuccessFactor.Workflow;

namespace SuccessFactor.Workflow.Security;

public class WorkflowAuthorizationService : ITransientDependency
{
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    private readonly IRepository<Cycle, Guid> _cycleRepo;
    private readonly IRepository<CycleParticipant, Guid> _participantRepo;

    private readonly IRepository<Employee, Guid> _employeeRepo;
    private readonly IRepository<EmployeeManager, Guid> _employeeManagerRepo;

    private readonly IRepository<PhaseRolePermission, Guid> _permRepo;
    private readonly IRepository<PhaseFieldPolicy, Guid> _policyRepo;

    public WorkflowAuthorizationService(
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        IClock clock,
        IRepository<Cycle, Guid> cycleRepo,
        IRepository<CycleParticipant, Guid> participantRepo,
        IRepository<Employee, Guid> employeeRepo,
        IRepository<EmployeeManager, Guid> employeeManagerRepo,
        IRepository<PhaseRolePermission, Guid> permRepo,
        IRepository<PhaseFieldPolicy, Guid> policyRepo)
    {
        _currentTenant = currentTenant;
        _currentUser = currentUser;
        _clock = clock;
        _cycleRepo = cycleRepo;
        _participantRepo = participantRepo;
        _employeeRepo = employeeRepo;
        _employeeManagerRepo = employeeManagerRepo;
        _permRepo = permRepo;
        _policyRepo = policyRepo;
    }

    public async Task<WorkflowAuthResult> EvaluateAsync(Guid cycleId, Guid targetEmployeeId, DateOnly? asOfDate = null)
    {
        if (_currentTenant.Id == null) throw new BusinessException("TenantMissing");
        if (_currentUser.Id == null) throw new BusinessException("UserNotAuthenticated");

        var date = asOfDate ?? DateOnly.FromDateTime(_clock.Now);

        // actor employee
        var actor = await _employeeRepo.FirstOrDefaultAsync(e => e.UserId == _currentUser.Id.Value)
                    ?? throw new BusinessException("EmployeeNotLinkedToUser");

        var cycle = await _cycleRepo.FirstOrDefaultAsync(c => c.Id == cycleId)
                   ?? throw new BusinessException("CycleNotFound");

        var participant = await _participantRepo.FirstOrDefaultAsync(p => p.CycleId == cycleId && p.EmployeeId == targetEmployeeId)
                         ?? throw new BusinessException("ParticipantNotFound");

        if (!participant.CurrentPhaseId.HasValue)
            throw new BusinessException("ParticipantHasNoPhase");

        var templateId = cycle.TemplateId;
        var phaseId = participant.CurrentPhaseId.Value;

        var roleCode = await ResolveRoleCodeForTargetAsync(actor.Id, targetEmployeeId, date);

        // permission effettivo (role o fallback "*")
        var perm = await _permRepo.FirstOrDefaultAsync(x =>
                      x.TemplateId == templateId && x.PhaseId == phaseId && x.RoleCode == roleCode)
                   ?? await _permRepo.FirstOrDefaultAsync(x =>
                      x.TemplateId == templateId && x.PhaseId == phaseId && x.RoleCode == "*");

        var canView = perm?.CanView ?? false;
        var canEdit = perm?.CanEdit ?? false;
        var canSubmit = perm?.CanSubmit ?? false;
        var canAdvance = perm?.CanAdvance ?? false;

        // policies effettive (role + fallback "*", role specific vince)
        var policies = await _policyRepo.GetListAsync(x =>
            x.TemplateId == templateId &&
            x.PhaseId == phaseId &&
            (x.RoleCode == roleCode || x.RoleCode == "*"));

        var effectivePolicies = policies
            .GroupBy(p => p.FieldKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.FirstOrDefault(p => p.RoleCode == roleCode) ?? g.First(p => p.RoleCode == "*"),
                StringComparer.OrdinalIgnoreCase
            );

        return new WorkflowAuthResult(
            cycleId,
            targetEmployeeId,
            templateId,
            phaseId,
            actor.Id,
            roleCode,
            canView,
            canEdit,
            canSubmit,
            canAdvance,
            effectivePolicies
        );
    }

    private async Task<string> ResolveRoleCodeForTargetAsync(Guid actorEmployeeId, Guid targetEmployeeId, DateOnly date)
    {
        var roles = (_currentUser.Roles ?? Array.Empty<string>()).ToArray();

        if (SuccessFactorRoles.IsAdminOrHr(roles))
            return "HR";

        if (actorEmployeeId == targetEmployeeId)
            return "Employee";

        bool isManagerOfTarget = await _employeeManagerRepo.AnyAsync(x =>
            x.ManagerEmployeeId == actorEmployeeId &&
            x.EmployeeId == targetEmployeeId &&
            (!x.StartDate.HasValue || x.StartDate.Value <= date) &&
            (!x.EndDate.HasValue || x.EndDate.Value >= date));

        if (isManagerOfTarget)
        {
            return "Manager";
        }

        throw new BusinessException("TargetEmployeeNotInManagerScope");
    }
}

public sealed class WorkflowAuthResult
{
    public Guid CycleId { get; }
    public Guid TargetEmployeeId { get; }
    public Guid TemplateId { get; }
    public Guid PhaseId { get; }

    public Guid ActorEmployeeId { get; }
    public string RoleCode { get; }

    public bool CanView { get; }
    public bool CanEdit { get; }
    public bool CanSubmit { get; }
    public bool CanAdvance { get; }

    // Effective policies by FieldKey (role-specific or "*")
    public IReadOnlyDictionary<string, PhaseFieldPolicy> Policies { get; }

    public WorkflowAuthResult(
        Guid cycleId,
        Guid targetEmployeeId,
        Guid templateId,
        Guid phaseId,
        Guid actorEmployeeId,
        string roleCode,
        bool canView,
        bool canEdit,
        bool canSubmit,
        bool canAdvance,
        IReadOnlyDictionary<string, PhaseFieldPolicy> policies)
    {
        CycleId = cycleId;
        TargetEmployeeId = targetEmployeeId;
        TemplateId = templateId;
        PhaseId = phaseId;
        ActorEmployeeId = actorEmployeeId;
        RoleCode = roleCode;
        CanView = canView;
        CanEdit = canEdit;
        CanSubmit = canSubmit;
        CanAdvance = canAdvance;
        Policies = policies;
    }

    public (string Access, bool IsRequired) GetPolicyOrDefault(string fieldKey)
    {
        if (Policies.TryGetValue(fieldKey, out var p))
            return (p.Access, p.IsRequired);

        // Default “dev friendly”: se non hai policy, non blocchiamo per campo.
        // La vera “chiusura” la fai quando seed-i policies.
        return ("Edit", false);
    }

    public void EnsureCanEdit()
    {
        if (!CanEdit) throw new BusinessException("PhaseEditNotAllowed");
    }

    public void EnsureCanAdvance()
    {
        if (!CanAdvance) throw new BusinessException("PhaseAdvanceNotAllowed");
    }

    public void EnsureFieldEditable(string fieldKey)
    {
        var (access, _) = GetPolicyOrDefault(fieldKey);
        if (!string.Equals(access, "Edit", StringComparison.OrdinalIgnoreCase))
            throw new BusinessException("FieldNotEditable").WithData("FieldKey", fieldKey).WithData("Access", access);
    }
}