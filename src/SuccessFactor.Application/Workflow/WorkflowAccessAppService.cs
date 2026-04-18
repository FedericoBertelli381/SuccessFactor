using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using SuccessFactor.Cycles;
using SuccessFactor.Employees;
using SuccessFactor.Security;

namespace SuccessFactor.Workflow;

public class WorkflowAccessAppService : ApplicationService
{
    private readonly IRepository<Cycle, Guid> _cycleRepo;
    private readonly IRepository<CycleParticipant, Guid> _participantRepo;
    private readonly IRepository<Employee, Guid> _employeeRepo;
    private readonly IRepository<EmployeeManager, Guid> _employeeManagerRepo;

    private readonly IRepository<PhaseRolePermission, Guid> _permRepo;
    private readonly IRepository<PhaseFieldPolicy, Guid> _policyRepo;

    public WorkflowAccessAppService(
        IRepository<Cycle, Guid> cycleRepo,
        IRepository<CycleParticipant, Guid> participantRepo,
        IRepository<Employee, Guid> employeeRepo,
        IRepository<EmployeeManager, Guid> employeeManagerRepo,
        IRepository<PhaseRolePermission, Guid> permRepo,
        IRepository<PhaseFieldPolicy, Guid> policyRepo)
    {
        _cycleRepo = cycleRepo;
        _participantRepo = participantRepo;
        _employeeRepo = employeeRepo;
        _employeeManagerRepo = employeeManagerRepo;
        _permRepo = permRepo;
        _policyRepo = policyRepo;
    }

    /// <summary>
    /// Ritorna permessi e field policies effettive per l'utente loggato (actor) rispetto a un dipendente target nel ciclo.
    /// </summary>
    public async Task<WorkflowAccessContextDto> GetAccessAsync(Guid cycleId, Guid targetEmployeeId, DateOnly? asOfDate = null)
    {
        EnsureTenantAndUser();

        var date = asOfDate ?? DateOnly.FromDateTime(Clock.Now);

        // actor employee
        var actor = await _employeeRepo.FirstOrDefaultAsync(e => e.UserId == CurrentUser.Id!.Value)
                    ?? throw new BusinessException("EmployeeNotLinkedToUser");

        var cycle = await _cycleRepo.FirstOrDefaultAsync(c => c.Id == cycleId)
                   ?? throw new BusinessException("CycleNotFound");

        var participant = await _participantRepo.FirstOrDefaultAsync(p => p.CycleId == cycleId && p.EmployeeId == targetEmployeeId)
                         ?? throw new BusinessException("ParticipantNotFound");

        if (!participant.CurrentPhaseId.HasValue)
            throw new BusinessException("ParticipantHasNoPhase");

        var templateId = cycle.TemplateId;
        var phaseId = participant.CurrentPhaseId.Value;

        // decide RoleCodeUsed (priorità: HR > Manager > Employee(Self) > Employee(generic))
        var roleCode = await ResolveRoleCodeForTargetAsync(actor.Id, targetEmployeeId, date);

        // permesso effettivo (role-specific o fallback "*")
        var perm = await _permRepo.FirstOrDefaultAsync(x =>
                      x.TemplateId == templateId && x.PhaseId == phaseId && x.RoleCode == roleCode)
                   ?? await _permRepo.FirstOrDefaultAsync(x =>
                      x.TemplateId == templateId && x.PhaseId == phaseId && x.RoleCode == "*");

        // default deny se non configurato
        bool canView = perm?.CanView ?? false;
        bool canEdit = perm?.CanEdit ?? false;
        bool canSubmit = perm?.CanSubmit ?? false;
        bool canAdvance = perm?.CanAdvance ?? false;

        // field policies effettive: roleCode + fallback "*", role-specific sovrascrive "*"
        var policies = await _policyRepo.GetListAsync(x =>
            x.TemplateId == templateId &&
            x.PhaseId == phaseId &&
            (x.RoleCode == roleCode || x.RoleCode == "*"));

        var effective = policies
            .GroupBy(p => p.FieldKey, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.FirstOrDefault(p => p.RoleCode == roleCode) ?? g.First(p => p.RoleCode == "*"))
            .Select(ObjectMapper.Map<PhaseFieldPolicy, PhaseFieldPolicyDto>)
            .ToList();

        return new WorkflowAccessContextDto
        {
            CycleId = cycleId,
            TargetEmployeeId = targetEmployeeId,
            TemplateId = templateId,
            PhaseId = phaseId,
            ActorEmployeeId = actor.Id,
            RoleCodeUsed = roleCode,
            CanView = canView,
            CanEdit = canEdit,
            CanSubmit = canSubmit,
            CanAdvance = canAdvance,
            FieldPolicies = effective
        };
    }

    private async Task<string> ResolveRoleCodeForTargetAsync(Guid actorEmployeeId, Guid targetEmployeeId, DateOnly date)
    {
        var abpRoles = (CurrentUser.Roles ?? Array.Empty<string>()).ToArray();

        // HR se ruolo ABP contiene "hr"
        if (SuccessFactorRoles.IsAdminOrHr(abpRoles))
            return "HR";

        // Self
        if (actorEmployeeId == targetEmployeeId)
            return "Employee";

        // Manager se relazione attiva manager->target
        bool isManagerOfTarget = await _employeeManagerRepo.AnyAsync(x =>
            x.ManagerEmployeeId == actorEmployeeId &&
            x.EmployeeId == targetEmployeeId &&
            (!x.StartDate.HasValue || x.StartDate.Value <= date) &&
            (!x.EndDate.HasValue || x.EndDate.Value >= date));

        return isManagerOfTarget ? "Manager" : "Employee";
    }

    private void EnsureTenantAndUser()
    {
        if (CurrentTenant.Id == null) throw new BusinessException("TenantMissing");
        if (CurrentUser.Id == null) throw new BusinessException("UserNotAuthenticated");
    }
}