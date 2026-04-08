using SuccessFactor.Cycles;
using SuccessFactor.Employees;
using SuccessFactor.Workflow;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using SuccessFactor.Workflow.Security;

namespace SuccessFactor.Cycles;

public class CycleWorkflowAppService : ApplicationService
{
    private readonly IRepository<Cycle, Guid> _cycleRepo;
    private readonly IRepository<CycleParticipant, Guid> _participantRepo;
    private readonly IRepository<Employee, Guid> _employeeRepo;

    private readonly IRepository<ProcessPhase, Guid> _phaseRepo;
    private readonly IRepository<PhaseTransition, Guid> _transitionRepo;
    private readonly WorkflowAuthorizationService _wfAuth;
    public CycleWorkflowAppService(
        IRepository<Cycle, Guid> cycleRepo,
        IRepository<CycleParticipant, Guid> participantRepo,
        IRepository<Employee, Guid> employeeRepo,
        IRepository<ProcessPhase, Guid> phaseRepo,
        IRepository<PhaseTransition, Guid> transitionRepo,
        WorkflowAuthorizationService wfAuth)
    {
        _cycleRepo = cycleRepo;
        _participantRepo = participantRepo;
        _employeeRepo = employeeRepo;
        _phaseRepo = phaseRepo;
        _transitionRepo = transitionRepo;
        _wfAuth = wfAuth;
    }

    public async Task<int> GenerateParticipantsAsync(GenerateParticipantsDto input)
    {
        EnsureTenant();

        var cycle = await _cycleRepo.FirstOrDefaultAsync(c => c.Id == input.CycleId);
        if (cycle == null) throw new BusinessException("CycleNotFound");

        // fase iniziale = PhaseOrder minimo del template
        var startPhase = await GetStartPhaseAsync(cycle.TemplateId);
        if (startPhase == null) throw new BusinessException("NoPhasesForTemplate");

        // elenco employee
        var employeeIds = input.EmployeeIds;
        if (employeeIds == null || employeeIds.Length == 0)
        {
            var emps = await _employeeRepo.GetListAsync(e => e.IsActive);
            employeeIds = emps.Select(e => e.Id).ToArray();
        }

        int created = 0;

        foreach (var empId in employeeIds.Distinct())
        {
            var exists = await _participantRepo.AnyAsync(p => p.CycleId == input.CycleId && p.EmployeeId == empId);
            if (exists) continue;

            var p = new CycleParticipant
            {
                TenantId = CurrentTenant.Id,
                CycleId = input.CycleId,
                EmployeeId = empId,
                CurrentPhaseId = startPhase.Id,
                Status = "Active"
            };

            await _participantRepo.InsertAsync(p, autoSave: true);
            created++;
        }

        return created;
    }

    public async Task AdvancePhaseAsync(AdvancePhaseDto input)
    {
        EnsureTenant();

        var cycle = await _cycleRepo.FirstOrDefaultAsync(c => c.Id == input.CycleId);
        if (cycle == null) throw new BusinessException("CycleNotFound");

        if (!string.Equals(cycle.Status, "Active", StringComparison.OrdinalIgnoreCase))
            throw new BusinessException("CycleNotActive");

        var participant = await _participantRepo.FirstOrDefaultAsync(p =>
            p.CycleId == input.CycleId && p.EmployeeId == input.EmployeeId);

        if (participant == null) throw new BusinessException("ParticipantNotFound");
        if (string.Equals(participant.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            throw new BusinessException("ParticipantAlreadyCompleted");

        var currentPhaseId = participant.CurrentPhaseId;
        var auth = await _wfAuth.EvaluateAsync(input.CycleId, input.EmployeeId);
        auth.EnsureCanAdvance();
        // se non ha phase -> assegna start
        if (!currentPhaseId.HasValue)
        {
            var start = await GetStartPhaseAsync(cycle.TemplateId) ?? throw new BusinessException("NoPhasesForTemplate");
            participant.CurrentPhaseId = start.Id;
            await _participantRepo.UpdateAsync(participant, autoSave: true);
            return;
        }

        var currentPhase = await _phaseRepo.GetAsync(currentPhaseId.Value);

        if (currentPhase.IsTerminal)
            throw new BusinessException("AlreadyInTerminalPhase");

        // calcola next
        var next = await ResolveNextPhaseAsync(cycle.TemplateId, currentPhase, input.ToPhaseId);

        participant.CurrentPhaseId = next.Id;

        if (next.IsTerminal)
            participant.Status = "Completed";

        await _participantRepo.UpdateAsync(participant, autoSave: true);
    }

    // ---------- helpers ----------

    private void EnsureTenant()
    {
        if (CurrentTenant.Id == null) throw new BusinessException("TenantMissing");
    }

    private async Task<ProcessPhase?> GetStartPhaseAsync(Guid templateId)
    {
        var q = await _phaseRepo.GetQueryableAsync();
        return await AsyncExecuter.FirstOrDefaultAsync(
            q.Where(p => p.TemplateId == templateId)
             .OrderBy(p => p.PhaseOrder)
        );
    }

    private async Task<ProcessPhase> ResolveNextPhaseAsync(Guid templateId, ProcessPhase current, Guid? toPhaseId)
    {
        // 1) se esistono transizioni, usare quelle
        var transQ = await _transitionRepo.GetQueryableAsync();
        var transitions = await AsyncExecuter.ToListAsync(
            transQ.Where(t => t.TemplateId == templateId && t.FromPhaseId == current.Id)
        );

        if (toPhaseId.HasValue)
        {
            // se ci sono transizioni, deve essere permessa
            if (transitions.Count > 0 && !transitions.Any(t => t.ToPhaseId == toPhaseId.Value))
                throw new BusinessException("TransitionNotAllowed");

            var target = await _phaseRepo.GetAsync(toPhaseId.Value);
            if (target.TemplateId != templateId) throw new BusinessException("ToPhaseNotInTemplate");
            return target;
        }

        if (transitions.Count > 0)
        {
            // scegli quella con PhaseOrder più basso (tra le candidate)
            var phaseQ = await _phaseRepo.GetQueryableAsync();
            var candidates = await AsyncExecuter.ToListAsync(
                phaseQ.Where(p => transitions.Select(t => t.ToPhaseId).Contains(p.Id))
            );
            return candidates.OrderBy(p => p.PhaseOrder).First();
        }

        // 2) fallback: ordine successivo
        var q = await _phaseRepo.GetQueryableAsync();
        var next = await AsyncExecuter.FirstOrDefaultAsync(
            q.Where(p => p.TemplateId == templateId && p.PhaseOrder > current.PhaseOrder)
             .OrderBy(p => p.PhaseOrder)
        );

        if (next == null) throw new BusinessException("NoNextPhase");
        return next;
    }
}