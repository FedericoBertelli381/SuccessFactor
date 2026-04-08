using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;

// tuoi namespace
using SuccessFactor.Process;
using SuccessFactor.Workflow;
using SuccessFactor.Cycles;
using SuccessFactor.Employees;
using SuccessFactor.Goals;
using SuccessFactor.Competencies;
using SuccessFactor.Competencies.Models;
using SuccessFactor.Competencies.Assessments;

namespace SuccessFactor.Data;

public class SuccessFactorDemoDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly ICurrentTenant _currentTenant;
    private readonly IClock _clock;

    private readonly IRepository<ProcessTemplate, Guid> _templateRepo;
    private readonly IRepository<ProcessPhase, Guid> _phaseRepo;
    private readonly IRepository<PhaseTransition, Guid> _transitionRepo;
    private readonly IRepository<PhaseRolePermission, Guid> _rolePermRepo;
    private readonly IRepository<PhaseFieldPolicy, Guid> _fieldPolicyRepo;

    private readonly IRepository<Cycle, Guid> _cycleRepo;
    private readonly IRepository<CycleParticipant, Guid> _participantRepo;

    private readonly IRepository<Employee, Guid> _employeeRepo;
    private readonly IRepository<EmployeeManager, Guid> _employeeManagerRepo;

    private readonly IRepository<Goal, Guid> _goalRepo;
    private readonly IRepository<GoalAssignment, Guid> _assignmentRepo;

    private readonly IRepository<Competency, Guid> _competencyRepo;
    private readonly IRepository<CompetencyModel, Guid> _modelRepo;
    private readonly IRepository<CompetencyModelItem, Guid> _modelItemRepo;

    private readonly IRepository<CompetencyAssessment, Guid> _assessmentRepo;
    private readonly IRepository<CompetencyAssessmentItem, Guid> _assessmentItemRepo;

    public SuccessFactorDemoDataSeedContributor(
        ICurrentTenant currentTenant,
        IClock clock,
        IRepository<ProcessTemplate, Guid> templateRepo,
        IRepository<ProcessPhase, Guid> phaseRepo,
        IRepository<PhaseTransition, Guid> transitionRepo,
        IRepository<PhaseRolePermission, Guid> rolePermRepo,
        IRepository<PhaseFieldPolicy, Guid> fieldPolicyRepo,
        IRepository<Cycle, Guid> cycleRepo,
        IRepository<CycleParticipant, Guid> participantRepo,
        IRepository<Employee, Guid> employeeRepo,
        IRepository<EmployeeManager, Guid> employeeManagerRepo,
        IRepository<Goal, Guid> goalRepo,
        IRepository<GoalAssignment, Guid> assignmentRepo,
        IRepository<Competency, Guid> competencyRepo,
        IRepository<CompetencyModel, Guid> modelRepo,
        IRepository<CompetencyModelItem, Guid> modelItemRepo,
        IRepository<CompetencyAssessment, Guid> assessmentRepo,
        IRepository<CompetencyAssessmentItem, Guid> assessmentItemRepo)
    {
        _currentTenant = currentTenant;
        _clock = clock;

        _templateRepo = templateRepo;
        _phaseRepo = phaseRepo;
        _transitionRepo = transitionRepo;
        _rolePermRepo = rolePermRepo;
        _fieldPolicyRepo = fieldPolicyRepo;

        _cycleRepo = cycleRepo;
        _participantRepo = participantRepo;

        _employeeRepo = employeeRepo;
        _employeeManagerRepo = employeeManagerRepo;

        _goalRepo = goalRepo;
        _assignmentRepo = assignmentRepo;

        _competencyRepo = competencyRepo;
        _modelRepo = modelRepo;
        _modelItemRepo = modelItemRepo;

        _assessmentRepo = assessmentRepo;
        _assessmentItemRepo = assessmentItemRepo;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        // Seed SOLO per tenant
        if (context.TenantId == null)
            return;

        using (_currentTenant.Change(context.TenantId))
        {
            // 1) Template + Phases + Transitions
            var template = await GetOrCreateTemplateAsync("Demo Performance Template", 1);

            var phaseSelf = await GetOrCreatePhaseAsync(template.Id, "SELF", "Self Assessment", 10, isTerminal: false);
            var phaseMgr = await GetOrCreatePhaseAsync(template.Id, "MGR", "Manager Review", 20, isTerminal: false);
            var phaseHr = await GetOrCreatePhaseAsync(template.Id, "HR", "HR Final", 30, isTerminal: true);

            await EnsureTransitionAsync(template.Id, phaseSelf.Id, phaseMgr.Id);
            await EnsureTransitionAsync(template.Id, phaseMgr.Id, phaseHr.Id);

            // 2) Permissions + Field policies (minimo)
            await SeedRolePermissionsAsync(template.Id, phaseSelf.Id, phaseMgr.Id, phaseHr.Id);
            await SeedFieldPoliciesAsync(template.Id, phaseSelf.Id, phaseMgr.Id, phaseHr.Id);

            // 3) Employees (demo se mancano) + manager relation
            var employees = await EnsureDemoEmployeesAsync();

            // 4) Cycle active + participants
            var year = DateTime.UtcNow.Year;
            var cycle = await GetOrCreateCycleAsync($"Demo Cycle {year}", year, template.Id);

            await EnsureParticipantsAsync(cycle.Id, phaseSelf.Id, employees);

            // 5) Goals + Assignments
            var g1 = await GetOrCreateGoalAsync("Aumentare qualità del servizio", "Demo goal", "Quality", isLibraryItem: true);
            var g2 = await GetOrCreateGoalAsync("Ridurre tempi di risposta", "Demo goal", "Efficiency", isLibraryItem: true);

            foreach (var e in employees)
            {
                await EnsureAssignmentAsync(cycle.Id, e.Id, g1.Id, weight: 50m);
                await EnsureAssignmentAsync(cycle.Id, e.Id, g2.Id, weight: 50m);
            }

            // 6) Competencies + model + assessment demo
            var c1 = await GetOrCreateCompetencyAsync("TEAMWORK", "Teamwork", "Collaborazione", true);
            var c2 = await GetOrCreateCompetencyAsync("OWNERSHIP", "Ownership", "Responsabilità", true);
            var c3 = await GetOrCreateCompetencyAsync("COMM", "Communication", "Comunicazione", true);

            var model = await GetOrCreateCompetencyModelAsync("Demo Competency Model", minScore: 1, maxScore: 5);
            await EnsureModelItemAsync(model.Id, c1.Id, isRequired: true, weight: null);
            await EnsureModelItemAsync(model.Id, c2.Id, isRequired: true, weight: null);
            await EnsureModelItemAsync(model.Id, c3.Id, isRequired: false, weight: null);

            // assessment self sul primo employee
            var firstEmp = employees.First();
            var assessment = await GetOrCreateAssessmentAsync(cycle.Id, firstEmp.Id, firstEmp.Id, model.Id, "Self");

            // crea items se mancano
            await EnsureAssessmentItemAsync(assessment.Id, c1.Id);
            await EnsureAssessmentItemAsync(assessment.Id, c2.Id);
            await EnsureAssessmentItemAsync(assessment.Id, c3.Id);
        }
    }

    // ---------------- helpers: Template / Phases / Transitions ----------------

    private async Task<ProcessTemplate> GetOrCreateTemplateAsync(string name, int version)
    {
        var existing = await _templateRepo.FirstOrDefaultAsync(t => t.Name == name && t.Version == version);
        if (existing != null) return existing;

        var t = new ProcessTemplate
        {
            TenantId = _currentTenant.Id,
            Name = name,
            Version = version,
            IsDefault = true
        };

        // se c'è già un default, lo spegniamo (coerenza)
        var defaults = await _templateRepo.GetListAsync(x => x.IsDefault);
        foreach (var d in defaults) d.IsDefault = false;
        foreach (var d in defaults) await _templateRepo.UpdateAsync(d, autoSave: true);

        await _templateRepo.InsertAsync(t, autoSave: true);
        return t;
    }

    private async Task<ProcessPhase> GetOrCreatePhaseAsync(Guid templateId, string code, string name, int order, bool isTerminal)
    {
        var existing = await _phaseRepo.FirstOrDefaultAsync(p => p.TemplateId == templateId && p.Code == code);
        if (existing != null) return existing;

        var p = new ProcessPhase
        {
            TemplateId = templateId,
            Code = code,
            Name = name,
            PhaseOrder = order,
            IsTerminal = isTerminal
        };

        await _phaseRepo.InsertAsync(p, autoSave: true);
        return p;
    }

    private async Task EnsureTransitionAsync(Guid templateId, Guid fromPhaseId, Guid toPhaseId)
    {
        var exists = await _transitionRepo.AnyAsync(t =>
            t.TemplateId == templateId && t.FromPhaseId == fromPhaseId && t.ToPhaseId == toPhaseId);

        if (exists) return;

        await _transitionRepo.InsertAsync(new PhaseTransition
        {
            TemplateId = templateId,
            FromPhaseId = fromPhaseId,
            ToPhaseId = toPhaseId
        }, autoSave: true);
    }

    // ---------------- helpers: permissions / field policies ----------------

    private async Task SeedRolePermissionsAsync(Guid templateId, Guid selfId, Guid mgrId, Guid hrId)
    {
        // SELF
        await EnsureRolePermAsync(templateId, selfId, "Employee", canView: true, canEdit: true, canSubmit: true, canAdvance: true);
        await EnsureRolePermAsync(templateId, selfId, "Manager", canView: true, canEdit: false, canSubmit: false, canAdvance: false);
        await EnsureRolePermAsync(templateId, selfId, "HR", canView: true, canEdit: false, canSubmit: false, canAdvance: false);

        // MGR
        await EnsureRolePermAsync(templateId, mgrId, "Employee", canView: true, canEdit: false, canSubmit: false, canAdvance: false);
        await EnsureRolePermAsync(templateId, mgrId, "Manager", canView: true, canEdit: true, canSubmit: true, canAdvance: true);
        await EnsureRolePermAsync(templateId, mgrId, "HR", canView: true, canEdit: false, canSubmit: false, canAdvance: false);

        // HR (terminal)
        await EnsureRolePermAsync(templateId, hrId, "Employee", canView: true, canEdit: false, canSubmit: false, canAdvance: false);
        await EnsureRolePermAsync(templateId, hrId, "Manager", canView: true, canEdit: false, canSubmit: false, canAdvance: false);
        await EnsureRolePermAsync(templateId, hrId, "HR", canView: true, canEdit: true, canSubmit: true, canAdvance: false);
    }

    private async Task EnsureRolePermAsync(Guid templateId, Guid phaseId, string roleCode,
        bool canView, bool canEdit, bool canSubmit, bool canAdvance)
    {
        var existing = await _rolePermRepo.FirstOrDefaultAsync(x =>
            x.TemplateId == templateId && x.PhaseId == phaseId && x.RoleCode == roleCode);

        if (existing != null) return;

        await _rolePermRepo.InsertAsync(new PhaseRolePermission
        {
            TemplateId = templateId,
            PhaseId = phaseId,
            RoleCode = roleCode,
            CanView = canView,
            CanEdit = canEdit,
            CanSubmit = canSubmit,
            CanAdvance = canAdvance
        }, autoSave: true);
    }

    private async Task SeedFieldPoliciesAsync(Guid templateId, Guid selfId, Guid mgrId, Guid hrId)
    {
        // FieldKeys (stringhe uguali a quelle che userai in enforcement)
        const string GP = "Goals.ProgressPercent";
        const string GA = "Goals.ActualValue";
        const string GN = "Goals.Note";
        const string CS = "Competencies.Score";
        const string CC = "Competencies.Comment";

        // SELF: Employee Edit, Manager/HR Read
        foreach (var f in new[] { GP, GA, GN, CS, CC })
        {
            await EnsureFieldPolicyAsync(templateId, selfId, f, "Employee", "Edit", isRequired: false);
            await EnsureFieldPolicyAsync(templateId, selfId, f, "Manager", "Read", isRequired: false);
            await EnsureFieldPolicyAsync(templateId, selfId, f, "HR", "Read", isRequired: false);
        }

        // MGR: Manager Edit, Employee/HR Read
        foreach (var f in new[] { GP, GA, GN, CS, CC })
        {
            await EnsureFieldPolicyAsync(templateId, mgrId, f, "Manager", "Edit", isRequired: false);
            await EnsureFieldPolicyAsync(templateId, mgrId, f, "Employee", "Read", isRequired: false);
            await EnsureFieldPolicyAsync(templateId, mgrId, f, "HR", "Read", isRequired: false);
        }

        // HR: HR Edit, altri Hidden
        foreach (var f in new[] { GP, GA, GN, CS, CC })
        {
            await EnsureFieldPolicyAsync(templateId, hrId, f, "HR", "Edit", isRequired: false);
            await EnsureFieldPolicyAsync(templateId, hrId, f, "Manager", "Hidden", isRequired: false);
            await EnsureFieldPolicyAsync(templateId, hrId, f, "Employee", "Hidden", isRequired: false);
        }
    }

    private async Task EnsureFieldPolicyAsync(Guid templateId, Guid phaseId, string fieldKey, string roleCode, string access, bool isRequired)
    {
        var existing = await _fieldPolicyRepo.FirstOrDefaultAsync(x =>
            x.TemplateId == templateId && x.PhaseId == phaseId && x.FieldKey == fieldKey && x.RoleCode == roleCode);

        if (existing != null) return;

        await _fieldPolicyRepo.InsertAsync(new PhaseFieldPolicy
        {
            TemplateId = templateId,
            PhaseId = phaseId,
            FieldKey = fieldKey,
            RoleCode = roleCode,
            Access = access,
            IsRequired = isRequired
        }, autoSave: true);
    }

    // ---------------- helpers: employees / manager relation ----------------

    private async Task<List<Employee>> EnsureDemoEmployeesAsync()
    {
        var list = await _employeeRepo.GetListAsync(e => e.IsActive);

        if (list.Count >= 2) return list;

        // crea 2 employee demo
        var e1 = await GetOrCreateEmployeeAsync("D001", "Demo Manager", "demo.manager@demo.local");
        var e2 = await GetOrCreateEmployeeAsync("D002", "Demo Employee", "demo.employee@demo.local");

        // relazione manager (Line) valida da oggi
        var today = DateOnly.FromDateTime(_clock.Now);
        var relExists = await _employeeManagerRepo.AnyAsync(x =>
            x.EmployeeId == e2.Id && x.ManagerEmployeeId == e1.Id && x.RelationType == "Line" && x.EndDate == null);

        if (!relExists)
        {
            await _employeeManagerRepo.InsertAsync(new EmployeeManager
            {
                TenantId = _currentTenant.Id,
                EmployeeId = e2.Id,
                ManagerEmployeeId = e1.Id,
                RelationType = "Line",
                IsPrimary = true,
                StartDate = today,
                EndDate = null
            }, autoSave: true);
        }

        return await _employeeRepo.GetListAsync(e => e.IsActive);
    }

    private async Task<Employee> GetOrCreateEmployeeAsync(string matricola, string fullName, string email)
    {
        var existing = await _employeeRepo.FirstOrDefaultAsync(e => e.Matricola == matricola);
        if (existing != null) return existing;

        var emp = new Employee
        {
            TenantId = _currentTenant.Id,
            Matricola = matricola,
            FullName = fullName,
            Email = email,
            IsActive = true
        };

        await _employeeRepo.InsertAsync(emp, autoSave: true);
        return emp;
    }

    // ---------------- helpers: cycle / participants ----------------

    private async Task<Cycle> GetOrCreateCycleAsync(string name, int year, Guid templateId)
    {
        var existing = await _cycleRepo.FirstOrDefaultAsync(c => c.Name == name);
        if (existing != null) return existing;

        var cycle = new Cycle
        {
            TenantId = _currentTenant.Id,
            Name = name,
            CycleYear = year,
            TemplateId = templateId,
            Status = "Active",
            StartDate = new DateOnly(year, 1, 1),
            EndDate = new DateOnly(year, 12, 31)
        };

        await _cycleRepo.InsertAsync(cycle, autoSave: true);
        return cycle;
    }

    private async Task EnsureParticipantsAsync(Guid cycleId, Guid startPhaseId, List<Employee> employees)
    {
        foreach (var e in employees)
        {
            var exists = await _participantRepo.AnyAsync(p => p.CycleId == cycleId && p.EmployeeId == e.Id);
            if (exists) continue;

            await _participantRepo.InsertAsync(new CycleParticipant
            {
                TenantId = _currentTenant.Id,
                CycleId = cycleId,
                EmployeeId = e.Id,
                CurrentPhaseId = startPhaseId,
                Status = "Active"
            }, autoSave: true);
        }
    }

    // ---------------- helpers: goals / assignments ----------------

    private async Task<Goal> GetOrCreateGoalAsync(string title, string? description, string? category, bool isLibraryItem)
    {
        var existing = await _goalRepo.FirstOrDefaultAsync(g => g.Title == title);
        if (existing != null) return existing;

        var g = new Goal
        {
            TenantId = _currentTenant.Id,
            Title = title,
            Description = description,
            Category = category,
            IsLibraryItem = isLibraryItem
        };

        await _goalRepo.InsertAsync(g, autoSave: true);
        return g;
    }

    private async Task EnsureAssignmentAsync(Guid cycleId, Guid employeeId, Guid goalId, decimal weight)
    {
        var exists = await _assignmentRepo.AnyAsync(a =>
            a.CycleId == cycleId && a.EmployeeId == employeeId && a.GoalId == goalId);

        if (exists) return;

        await _assignmentRepo.InsertAsync(new GoalAssignment
        {
            TenantId = _currentTenant.Id,
            CycleId = cycleId,
            EmployeeId = employeeId,
            GoalId = goalId,
            Weight = weight,
            Status = "Draft"
        }, autoSave: true);
    }

    // ---------------- helpers: competencies ----------------

    private async Task<Competency> GetOrCreateCompetencyAsync(string code, string name, string? description, bool isActive)
    {
        var existing = await _competencyRepo.FirstOrDefaultAsync(c => c.Code == code);
        if (existing != null) return existing;

        var c = new Competency
        {
            TenantId = _currentTenant.Id,
            Code = code,
            Name = name,
            Description = description,
            IsActive = isActive
        };

        await _competencyRepo.InsertAsync(c, autoSave: true);
        return c;
    }

    private async Task<CompetencyModel> GetOrCreateCompetencyModelAsync(string name, int minScore, int maxScore)
    {
        var existing = await _modelRepo.FirstOrDefaultAsync(m => m.Name == name);
        if (existing != null) return existing;

        var m = new CompetencyModel
        {
            TenantId = _currentTenant.Id,
            Name = name,
            ScaleType = "Numeric",
            MinScore = minScore,
            MaxScore = maxScore
        };

        await _modelRepo.InsertAsync(m, autoSave: true);
        return m;
    }

    private async Task EnsureModelItemAsync(Guid modelId, Guid competencyId, bool isRequired, decimal? weight)
    {
        var exists = await _modelItemRepo.AnyAsync(i => i.ModelId == modelId && i.CompetencyId == competencyId);
        if (exists) return;

        await _modelItemRepo.InsertAsync(new CompetencyModelItem
        {
            TenantId = _currentTenant.Id,
            ModelId = modelId,
            CompetencyId = competencyId,
            IsRequired = isRequired,
            Weight = weight
        }, autoSave: true);
    }

    private async Task<CompetencyAssessment> GetOrCreateAssessmentAsync(Guid cycleId, Guid employeeId, Guid evaluatorEmployeeId, Guid modelId, string type)
    {
        var existing = await _assessmentRepo.FirstOrDefaultAsync(a =>
            a.CycleId == cycleId &&
            a.EmployeeId == employeeId &&
            a.EvaluatorEmployeeId == evaluatorEmployeeId &&
            a.AssessmentType == type);

        if (existing != null) return existing;

        var a = new CompetencyAssessment
        {
            TenantId = _currentTenant.Id,
            CycleId = cycleId,
            EmployeeId = employeeId,
            EvaluatorEmployeeId = evaluatorEmployeeId,
            ModelId = modelId,
            AssessmentType = type,
            Status = "Draft"
        };

        await _assessmentRepo.InsertAsync(a, autoSave: true);
        return a;
    }

    private async Task EnsureAssessmentItemAsync(Guid assessmentId, Guid competencyId)
    {
        var exists = await _assessmentItemRepo.AnyAsync(i => i.AssessmentId == assessmentId && i.CompetencyId == competencyId);
        if (exists) return;

        await _assessmentItemRepo.InsertAsync(new CompetencyAssessmentItem
        {
            TenantId = _currentTenant.Id,
            AssessmentId = assessmentId,
            CompetencyId = competencyId
        }, autoSave: true);
    }
}