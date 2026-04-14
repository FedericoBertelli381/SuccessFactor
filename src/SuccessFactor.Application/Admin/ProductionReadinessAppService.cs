using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SuccessFactor.Competencies.Assessments;
using SuccessFactor.Cycles;
using SuccessFactor.Employees;
using SuccessFactor.Process;
using SuccessFactor.Workflow;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Users;

namespace SuccessFactor.Admin;

[Authorize]
public class ProductionReadinessAppService : ApplicationService, IProductionReadinessAppService
{
    private static readonly string[] AllowedFieldAccess = ["Hidden", "Read", "Edit"];

    private readonly ICurrentUser _currentUser;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IRepository<Employee, Guid> _employeeRepository;
    private readonly IRepository<EmployeeManager, Guid> _managerRelationRepository;
    private readonly IRepository<Cycle, Guid> _cycleRepository;
    private readonly IRepository<CycleParticipant, Guid> _participantRepository;
    private readonly IRepository<ProcessTemplate, Guid> _templateRepository;
    private readonly IRepository<ProcessPhase, Guid> _phaseRepository;
    private readonly IRepository<PhaseFieldPolicy, Guid> _fieldPolicyRepository;
    private readonly IRepository<CompetencyAssessment, Guid> _assessmentRepository;

    public ProductionReadinessAppService(
        ICurrentUser currentUser,
        IAsyncQueryableExecuter asyncExecuter,
        IRepository<Employee, Guid> employeeRepository,
        IRepository<EmployeeManager, Guid> managerRelationRepository,
        IRepository<Cycle, Guid> cycleRepository,
        IRepository<CycleParticipant, Guid> participantRepository,
        IRepository<ProcessTemplate, Guid> templateRepository,
        IRepository<ProcessPhase, Guid> phaseRepository,
        IRepository<PhaseFieldPolicy, Guid> fieldPolicyRepository,
        IRepository<CompetencyAssessment, Guid> assessmentRepository)
    {
        _currentUser = currentUser;
        _asyncExecuter = asyncExecuter;
        _employeeRepository = employeeRepository;
        _managerRelationRepository = managerRelationRepository;
        _cycleRepository = cycleRepository;
        _participantRepository = participantRepository;
        _templateRepository = templateRepository;
        _phaseRepository = phaseRepository;
        _fieldPolicyRepository = fieldPolicyRepository;
        _assessmentRepository = assessmentRepository;
    }

    public async Task<ProductionReadinessDto> GetAsync()
    {
        EnsureTenantAndAdmin();

        var today = DateOnly.FromDateTime(Clock.Now);

        var employees = await _asyncExecuter.ToListAsync((await _employeeRepository.GetQueryableAsync()).Where(x => x.IsActive));
        var cycles = await _asyncExecuter.ToListAsync(await _cycleRepository.GetQueryableAsync());
        var participants = await _asyncExecuter.ToListAsync(await _participantRepository.GetQueryableAsync());
        var managerRelations = await _asyncExecuter.ToListAsync(await _managerRelationRepository.GetQueryableAsync());
        var templates = await _asyncExecuter.ToListAsync(await _templateRepository.GetQueryableAsync());
        var templateIds = templates.Select(x => x.Id).ToHashSet();
        var phases = await _asyncExecuter.ToListAsync(
            (await _phaseRepository.GetQueryableAsync()).Where(x => templateIds.Contains(x.TemplateId)));
        var fieldPolicies = await _asyncExecuter.ToListAsync(
            (await _fieldPolicyRepository.GetQueryableAsync()).Where(x => templateIds.Contains(x.TemplateId)));
        var assessments = await _asyncExecuter.ToListAsync(await _assessmentRepository.GetQueryableAsync());

        var activeCycles = cycles
            .Where(x => string.Equals(x.Status, "Active", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var activeManagerRelations = managerRelations
            .Where(x => (!x.StartDate.HasValue || x.StartDate.Value <= today) &&
                        (!x.EndDate.HasValue || x.EndDate.Value >= today))
            .ToList();
        var activeParticipants = participants
            .Where(x => string.Equals(x.Status, "Active", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var invalidFieldAccessCount = fieldPolicies
            .Count(x => !AllowedFieldAccess.Contains(x.Access, StringComparer.OrdinalIgnoreCase));
        var participantEmployeeIds = activeParticipants.Select(x => x.EmployeeId).ToHashSet();
        var employeesWithoutParticipant = employees.Count(x => activeCycles.Count > 0 && !participantEmployeeIds.Contains(x.Id));
        var participantCycleIds = participants.Select(x => x.CycleId).ToHashSet();
        var activeCyclesWithoutParticipants = activeCycles.Count(x => !participantCycleIds.Contains(x.Id));

        var dto = new ProductionReadinessDto
        {
            TenantId = CurrentTenant.Id?.ToString() ?? string.Empty,
            UserName = _currentUser.UserName ?? string.Empty,
            EmployeeCount = employees.Count,
            LinkedEmployeeCount = employees.Count(x => x.UserId.HasValue),
            ActiveCycleCount = activeCycles.Count,
            ActiveManagerRelationCount = activeManagerRelations.Count,
            ActiveParticipantCount = activeParticipants.Count,
            WorkflowTemplateCount = templates.Count,
            WorkflowPhaseCount = phases.Count,
            WorkflowFieldPolicyCount = fieldPolicies.Count
        };

        AddCheck(dto, "Tenant", "Tenant corrente", "Ok", $"TenantId: {dto.TenantId}");
        AddCheck(dto, "Auth", "Ruolo admin", "Ok", "Utente admin autorizzato alla readiness.");
        AddCheck(dto, "Workflow", "Template workflow", templates.Count > 0 ? "Ok" : "Error", templates.Count > 0 ? $"{templates.Count} template presenti." : "Nessun template workflow presente.");
        AddCheck(dto, "Workflow", "Fasi workflow", phases.Count > 0 ? "Ok" : "Error", phases.Count > 0 ? $"{phases.Count} fasi presenti." : "Nessuna fase workflow presente.");
        AddCheck(dto, "Workflow", "Field access string", invalidFieldAccessCount == 0 ? "Ok" : "Error", invalidFieldAccessCount == 0 ? "Tutte le field policy usano Hidden/Read/Edit." : $"{invalidFieldAccessCount} field policy hanno Access non valido.");
        AddCheck(dto, "Cycles", "Ciclo attivo", activeCycles.Count > 0 ? "Ok" : "Error", activeCycles.Count > 0 ? $"{activeCycles.Count} cicli attivi." : "Nessun ciclo Active presente.");
        AddCheck(dto, "Cycles", "Partecipanti ciclo", activeParticipants.Count > 0 ? "Ok" : "Warning", activeParticipants.Count > 0 ? $"{activeParticipants.Count} participant attivi." : "Nessun participant attivo presente.");
        AddCheck(dto, "Cycles", "Cicli attivi con participant", activeCyclesWithoutParticipants == 0 ? "Ok" : "Warning", activeCyclesWithoutParticipants == 0 ? "Ogni ciclo attivo ha almeno un participant." : $"{activeCyclesWithoutParticipants} cicli attivi senza participant.");
        AddCheck(dto, "Employees", "Employee attivi", employees.Count > 0 ? "Ok" : "Error", employees.Count > 0 ? $"{employees.Count} employee attivi." : "Nessun employee attivo.");
        AddCheck(dto, "Employees", "User link", dto.LinkedEmployeeCount > 0 ? "Ok" : "Warning", dto.LinkedEmployeeCount > 0 ? $"{dto.LinkedEmployeeCount} employee collegati a utenti." : "Nessun employee collegato a user ABP.");
        AddCheck(dto, "Employees", "Employee fuori ciclo", employeesWithoutParticipant == 0 ? "Ok" : "Warning", employeesWithoutParticipant == 0 ? "Tutti gli employee attivi sono participant del ciclo attivo." : $"{employeesWithoutParticipant} employee attivi non risultano participant attivi.");
        AddCheck(dto, "Managers", "Relazioni manager attive", activeManagerRelations.Count > 0 ? "Ok" : "Warning", activeManagerRelations.Count > 0 ? $"{activeManagerRelations.Count} relazioni manager attive." : "Nessuna relazione manager attiva.");
        AddCheck(dto, "Assessments", "Assessment presenti", assessments.Count > 0 ? "Ok" : "Warning", assessments.Count > 0 ? $"{assessments.Count} assessment presenti." : "Nessun assessment presente.");

        dto.ErrorCount = dto.Checks.Count(x => x.Status == "Error");
        dto.WarningCount = dto.Checks.Count(x => x.Status == "Warning");

        return dto;
    }

    private void EnsureTenantAndAdmin()
    {
        if (CurrentTenant.Id is null)
        {
            throw new BusinessException("TenantMissing");
        }

        var roles = _currentUser.Roles ?? Array.Empty<string>();

        if (!roles.Any(x => x.Contains("admin", StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessException("CurrentUserIsNotAdmin");
        }
    }

    private static void AddCheck(ProductionReadinessDto dto, string area, string check, string status, string message)
    {
        dto.Checks.Add(new ReadinessCheckDto
        {
            Area = area,
            Check = check,
            Status = status,
            Message = message
        });
    }
}
