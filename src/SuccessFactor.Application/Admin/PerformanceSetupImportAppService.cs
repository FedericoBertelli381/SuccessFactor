using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SuccessFactor.Auditing;
using SuccessFactor.Competencies;
using SuccessFactor.Security;
using SuccessFactor.Competencies.Assessments;
using SuccessFactor.Competencies.Models;
using SuccessFactor.Cycles;
using SuccessFactor.Employees;
using SuccessFactor.Goals;
using SuccessFactor.JobRoles;
using SuccessFactor.OrgUnits;
using SuccessFactor.Workflow;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Users;

namespace SuccessFactor.Admin;

[Authorize]
public class PerformanceSetupImportAppService : ApplicationService, IPerformanceSetupImportAppService
{
    private static readonly string[] AllowedParticipantStatuses = ["Active", "Completed", "Excluded"];
    private static readonly string[] AllowedRelationTypes = ["Line", "Functional", "Project", "Hr"];
    private static readonly string[] AllowedAssignmentStatuses = ["Draft", "Approved", "InProgress", "Closed"];

    private readonly ICurrentUser _currentUser;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IRepository<Cycle, Guid> _cycleRepository;
    private readonly IRepository<CycleParticipant, Guid> _participantRepository;
    private readonly IRepository<Employee, Guid> _employeeRepository;
    private readonly IRepository<EmployeeManager, Guid> _managerRelationRepository;
    private readonly IRepository<ProcessPhase, Guid> _phaseRepository;
    private readonly IRepository<OrgUnit, Guid> _orgUnitRepository;
    private readonly IRepository<JobRole, Guid> _jobRoleRepository;
    private readonly IRepository<Goal, Guid> _goalRepository;
    private readonly IRepository<GoalAssignment, Guid> _goalAssignmentRepository;
    private readonly IRepository<Competency, Guid> _competencyRepository;
    private readonly IRepository<CompetencyModel, Guid> _competencyModelRepository;
    private readonly IRepository<CompetencyModelItem, Guid> _competencyModelItemRepository;
    private readonly IRepository<CompetencyAssessment, Guid> _assessmentRepository;
    private readonly IBusinessAuditLogger _auditLogger;

    public PerformanceSetupImportAppService(
        ICurrentUser currentUser,
        IAsyncQueryableExecuter asyncExecuter,
        IRepository<Cycle, Guid> cycleRepository,
        IRepository<CycleParticipant, Guid> participantRepository,
        IRepository<Employee, Guid> employeeRepository,
        IRepository<EmployeeManager, Guid> managerRelationRepository,
        IRepository<ProcessPhase, Guid> phaseRepository,
        IRepository<OrgUnit, Guid> orgUnitRepository,
        IRepository<JobRole, Guid> jobRoleRepository,
        IRepository<Goal, Guid> goalRepository,
        IRepository<GoalAssignment, Guid> goalAssignmentRepository,
        IRepository<Competency, Guid> competencyRepository,
        IRepository<CompetencyModel, Guid> competencyModelRepository,
        IRepository<CompetencyModelItem, Guid> competencyModelItemRepository,
        IRepository<CompetencyAssessment, Guid> assessmentRepository,
        IBusinessAuditLogger auditLogger)
    {
        _currentUser = currentUser;
        _asyncExecuter = asyncExecuter;
        _cycleRepository = cycleRepository;
        _participantRepository = participantRepository;
        _employeeRepository = employeeRepository;
        _managerRelationRepository = managerRelationRepository;
        _phaseRepository = phaseRepository;
        _orgUnitRepository = orgUnitRepository;
        _jobRoleRepository = jobRoleRepository;
        _goalRepository = goalRepository;
        _goalAssignmentRepository = goalAssignmentRepository;
        _competencyRepository = competencyRepository;
        _competencyModelRepository = competencyModelRepository;
        _competencyModelItemRepository = competencyModelItemRepository;
        _assessmentRepository = assessmentRepository;
        _auditLogger = auditLogger;
    }

    public async Task<PerformanceSetupImportResultDto> ImportAsync(ImportPerformanceSetupInput input)
    {
        EnsureTenantAndAdmin();

        if (input is null || ImportContentsAreEmpty(input))
        {
            throw new BusinessException("PerformanceSetupImportContentRequired");
        }

        var cycles = await _asyncExecuter.ToListAsync(await _cycleRepository.GetQueryableAsync());
        var cycleByName = cycles.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var cycleById = cycles.ToDictionary(x => x.Id);

        var employees = await _asyncExecuter.ToListAsync(await _employeeRepository.GetQueryableAsync());
        var activeEmployeeByMatricola = employees
            .Where(x => x.IsActive)
            .ToDictionary(x => x.Matricola, StringComparer.OrdinalIgnoreCase);

        var orgUnitByName = (await _asyncExecuter.ToListAsync(await _orgUnitRepository.GetQueryableAsync()))
            .ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var jobRoleByName = (await _asyncExecuter.ToListAsync(await _jobRoleRepository.GetQueryableAsync()))
            .ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        var existingParticipants = await _asyncExecuter.ToListAsync(await _participantRepository.GetQueryableAsync());
        var participantByKey = existingParticipants.ToDictionary(x => ParticipantKey(x.CycleId, x.EmployeeId));

        var existingRelations = await _asyncExecuter.ToListAsync(await _managerRelationRepository.GetQueryableAsync());
        var relationByKey = existingRelations.ToDictionary(x => RelationKey(x.EmployeeId, x.ManagerEmployeeId, x.RelationType));

        var phases = await _asyncExecuter.ToListAsync(await _phaseRepository.GetQueryableAsync());
        var phasesByTemplate = phases
            .GroupBy(x => x.TemplateId)
            .ToDictionary(
                x => x.Key,
                x => x.ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase));

        var goals = await _asyncExecuter.ToListAsync(await _goalRepository.GetQueryableAsync());
        var goalById = goals.ToDictionary(x => x.Id);
        var goalByTitle = BuildUniqueDictionary(goals, x => x.Title);

        var assignments = await _asyncExecuter.ToListAsync(await _goalAssignmentRepository.GetQueryableAsync());
        var assignmentByKey = assignments.ToDictionary(x => GoalAssignmentKey(x.CycleId, x.EmployeeId, x.GoalId));

        var competencyByCode = (await _asyncExecuter.ToListAsync(await _competencyRepository.GetQueryableAsync()))
            .ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
        var modelByName = (await _asyncExecuter.ToListAsync(await _competencyModelRepository.GetQueryableAsync()))
            .ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var modelItemByKey = (await _asyncExecuter.ToListAsync(await _competencyModelItemRepository.GetQueryableAsync()))
            .ToDictionary(x => CompetencyModelItemKey(x.ModelId, x.CompetencyId));
        var usedModelIds = (await _asyncExecuter.ToListAsync(
                (await _assessmentRepository.GetQueryableAsync())
                    .Where(x => x.ModelId.HasValue)
                    .Select(x => x.ModelId!.Value)))
            .ToHashSet();

        var result = new PerformanceSetupImportResultDto();
        var orgUnitRows = ParseOrgUnitRows(input.OrgUnitsContent);
        var jobRoleRows = ParseJobRoleRows(input.JobRolesContent);
        var participantRows = ParseParticipantRows(input.ParticipantsContent);
        var relationRows = ParseManagerRelationRows(input.ManagerRelationsContent);
        var goalAssignmentRows = ParseGoalAssignmentRows(input.GoalAssignmentsContent);
        var competencyRows = ParseCompetencyRows(input.CompetenciesContent);
        var competencyModelRows = ParseCompetencyModelRows(input.CompetencyModelsContent);

        var validOrgUnitRows = new List<OrgUnitImportRow>();
        var validJobRoleRows = new List<JobRoleImportRow>();
        var validParticipantRows = new List<ParticipantImportRow>();
        var validRelationRows = new List<ManagerRelationImportRow>();
        var validGoalAssignmentRows = new List<GoalAssignmentImportRow>();
        var validCompetencyRows = new List<CompetencyImportRow>();
        var validCompetencyModelRows = new List<CompetencyModelImportRow>();

        ValidateOrgUnitRows(orgUnitRows, input.UpdateExisting, orgUnitByName, result, validOrgUnitRows);
        ValidateJobRoleRows(jobRoleRows, input.UpdateExisting, jobRoleByName, result, validJobRoleRows);
        ValidateParticipantRows(participantRows, input.UpdateExisting, cycleByName, cycleById, activeEmployeeByMatricola, phasesByTemplate, participantByKey, result, validParticipantRows);
        ValidateManagerRelationRows(relationRows, input.UpdateExisting, activeEmployeeByMatricola, relationByKey, result, validRelationRows);
        ValidateGoalAssignmentRows(goalAssignmentRows, input.UpdateExisting, cycleByName, cycleById, activeEmployeeByMatricola, participantByKey, goalById, goalByTitle, assignmentByKey, result, validGoalAssignmentRows);
        ValidateCompetencyRows(competencyRows, input.UpdateExisting, competencyByCode, result, validCompetencyRows);
        ValidateCompetencyModelRows(competencyModelRows, input.UpdateExisting, modelByName, competencyByCode, modelItemByKey, usedModelIds, result, validCompetencyModelRows);

        result.ErrorCount = result.Rows.Count(x => x.Status == "Error");
        result.HasErrors = result.ErrorCount > 0;

        if (result.HasErrors)
        {
            return result;
        }

        await ApplyOrgUnitRowsAsync(validOrgUnitRows, orgUnitByName, result);
        await ApplyJobRoleRowsAsync(validJobRoleRows, jobRoleByName, result);
        await ApplyCompetencyRowsAsync(validCompetencyRows, competencyByCode, result);
        await ApplyCompetencyModelRowsAsync(validCompetencyModelRows, modelByName, competencyByCode, modelItemByKey, result);
        await ApplyParticipantRowsAsync(validParticipantRows, participantByKey, result);
        await ApplyManagerRelationRowsAsync(validRelationRows, relationByKey, result);
        await ApplyGoalAssignmentRowsAsync(validGoalAssignmentRows, assignmentByKey, result);

        if (CurrentUnitOfWork is null)
        {
            throw new BusinessException("UnitOfWorkMissing");
        }

        await CurrentUnitOfWork.SaveChangesAsync();
        await _auditLogger.LogAsync("PerformanceSetupImportCompleted", "PerformanceSetupImport", null, new Dictionary<string, object?>
        {
            ["UpdateExisting"] = input.UpdateExisting,
            ["CreatedOrgUnits"] = result.CreatedOrgUnits,
            ["UpdatedOrgUnits"] = result.UpdatedOrgUnits,
            ["CreatedJobRoles"] = result.CreatedJobRoles,
            ["UpdatedJobRoles"] = result.UpdatedJobRoles,
            ["CreatedParticipants"] = result.CreatedParticipants,
            ["UpdatedParticipants"] = result.UpdatedParticipants,
            ["CreatedManagerRelations"] = result.CreatedManagerRelations,
            ["UpdatedManagerRelations"] = result.UpdatedManagerRelations,
            ["CreatedGoalAssignments"] = result.CreatedGoalAssignments,
            ["UpdatedGoalAssignments"] = result.UpdatedGoalAssignments,
            ["CreatedCompetencies"] = result.CreatedCompetencies,
            ["UpdatedCompetencies"] = result.UpdatedCompetencies,
            ["CreatedCompetencyModels"] = result.CreatedCompetencyModels,
            ["UpdatedCompetencyModels"] = result.UpdatedCompetencyModels,
            ["CreatedCompetencyModelItems"] = result.CreatedCompetencyModelItems,
            ["UpdatedCompetencyModelItems"] = result.UpdatedCompetencyModelItems,
            ["RowsCount"] = result.Rows.Count
        });
        return result;
    }

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

    private static bool ImportContentsAreEmpty(ImportPerformanceSetupInput input)
        => string.IsNullOrWhiteSpace(input.OrgUnitsContent) &&
           string.IsNullOrWhiteSpace(input.JobRolesContent) &&
           string.IsNullOrWhiteSpace(input.ParticipantsContent) &&
           string.IsNullOrWhiteSpace(input.ManagerRelationsContent) &&
           string.IsNullOrWhiteSpace(input.GoalAssignmentsContent) &&
           string.IsNullOrWhiteSpace(input.CompetenciesContent) &&
           string.IsNullOrWhiteSpace(input.CompetencyModelsContent);

    private static Dictionary<string, T> BuildUniqueDictionary<T>(List<T> rows, Func<T, string> keySelector)
    {
        return rows
            .GroupBy(keySelector, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() == 1)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
    }

    private void ValidateOrgUnitRows(
        List<OrgUnitImportRow> rows,
        bool updateExisting,
        Dictionary<string, OrgUnit> orgUnitByName,
        PerformanceSetupImportResultDto result,
        List<OrgUnitImportRow> validRows)
    {
        foreach (var row in rows)
        {
            row.Name = NormalizeRequiredOrEmpty(row.Name);
            row.ParentName = NormalizeOptionalImportValue(row.ParentName);
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var namesInFile = rows.Select(x => x.Name).Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var parentNameByName = rows
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => NormalizeOptionalImportValue(x.Last().ParentName), StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var message = string.IsNullOrWhiteSpace(row.Name) ? "Name obbligatorio." : null;

            if (message is null && !seen.Add(row.Name))
            {
                message = "OrgUnit duplicata nel file.";
            }
            else if (message is null && !updateExisting && orgUnitByName.ContainsKey(row.Name))
            {
                message = "OrgUnit gia esistente e update disabilitato.";
            }
            else if (message is null && row.ParentName is not null)
            {
                if (string.Equals(row.Name, row.ParentName, StringComparison.OrdinalIgnoreCase))
                {
                    message = "OrgUnit non puo essere parent di se stessa.";
                }
                else if (!orgUnitByName.ContainsKey(row.ParentName) && !namesInFile.Contains(row.ParentName))
                {
                    message = "ParentName non trovato.";
                }
                else if (WouldCreateOrgUnitLoop(row.Name, row.ParentName, parentNameByName, orgUnitByName))
                {
                    message = "Gerarchia OrgUnit ciclica.";
                }
            }

            AddRowResult(result, "OrgUnits", row.RowNumber, row.Name, message is null ? (orgUnitByName.ContainsKey(row.Name) ? "Update" : "Create") : "Error", message);

            if (message is null)
            {
                validRows.Add(row);
            }
        }
    }

    private static bool WouldCreateOrgUnitLoop(
        string name,
        string? parentName,
        Dictionary<string, string?> parentNameByName,
        Dictionary<string, OrgUnit> orgUnitByName)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { name };
        var currentParentName = parentName;

        while (!string.IsNullOrWhiteSpace(currentParentName))
        {
            if (!visited.Add(currentParentName))
            {
                return true;
            }

            if (parentNameByName.TryGetValue(currentParentName, out var fileParentName))
            {
                currentParentName = fileParentName;
                continue;
            }

            if (!orgUnitByName.TryGetValue(currentParentName, out var parent) ||
                !parent.ParentOrgUnitId.HasValue)
            {
                return false;
            }

            currentParentName = orgUnitByName.Values
                .FirstOrDefault(x => x.Id == parent.ParentOrgUnitId.Value)
                ?.Name;
        }

        return false;
    }

    private static void ValidateJobRoleRows(
        List<JobRoleImportRow> rows,
        bool updateExisting,
        Dictionary<string, JobRole> jobRoleByName,
        PerformanceSetupImportResultDto result,
        List<JobRoleImportRow> validRows)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            row.Name = NormalizeRequiredOrEmpty(row.Name);
            var message = string.IsNullOrWhiteSpace(row.Name) ? "Name obbligatorio." : null;

            if (message is null && !seen.Add(row.Name))
            {
                message = "JobRole duplicato nel file.";
            }
            else if (message is null && !updateExisting && jobRoleByName.ContainsKey(row.Name))
            {
                message = "JobRole gia esistente e update disabilitato.";
            }

            AddRowResult(result, "JobRoles", row.RowNumber, row.Name, message is null ? (jobRoleByName.ContainsKey(row.Name) ? "Update" : "Create") : "Error", message);

            if (message is null)
            {
                validRows.Add(row);
            }
        }
    }

    private void ValidateParticipantRows(
        List<ParticipantImportRow> rows,
        bool updateExisting,
        Dictionary<string, Cycle> cycleByName,
        Dictionary<Guid, Cycle> cycleById,
        Dictionary<string, Employee> activeEmployeeByMatricola,
        Dictionary<Guid, Dictionary<string, ProcessPhase>> phasesByTemplate,
        Dictionary<string, CycleParticipant> participantByKey,
        PerformanceSetupImportResultDto result,
        List<ParticipantImportRow> validRows)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var message = ResolveParticipantRow(row, cycleByName, cycleById, activeEmployeeByMatricola, phasesByTemplate);

            if (message is null)
            {
                var key = ParticipantKey(row.CycleId!.Value, row.EmployeeId!.Value);

                if (!seen.Add(key))
                {
                    message = "Participant duplicato nel file.";
                }
                else if (!updateExisting && participantByKey.ContainsKey(key))
                {
                    message = "Participant gia esistente e update disabilitato.";
                }
            }

            AddRowResult(
                result,
                "Participants",
                row.RowNumber,
                $"{row.Cycle};{row.EmployeeMatricola}",
                message is null ? (participantByKey.ContainsKey(ParticipantKey(row.CycleId!.Value, row.EmployeeId!.Value)) ? "Update" : "Create") : "Error",
                message);

            if (message is null)
            {
                validRows.Add(row);
            }
        }
    }

    private static string? ResolveParticipantRow(
        ParticipantImportRow row,
        Dictionary<string, Cycle> cycleByName,
        Dictionary<Guid, Cycle> cycleById,
        Dictionary<string, Employee> activeEmployeeByMatricola,
        Dictionary<Guid, Dictionary<string, ProcessPhase>> phasesByTemplate)
    {
        if (string.IsNullOrWhiteSpace(row.Cycle))
        {
            return "Cycle obbligatorio.";
        }

        if (!TryResolveCycle(row.Cycle, cycleByName, cycleById, out var cycle))
        {
            return "Cycle non trovato.";
        }

        if (string.IsNullOrWhiteSpace(row.EmployeeMatricola))
        {
            return "EmployeeMatricola obbligatoria.";
        }

        if (!activeEmployeeByMatricola.TryGetValue(row.EmployeeMatricola, out var employee))
        {
            return "Employee non trovato o non attivo.";
        }

        row.CycleId = cycle.Id;
        row.EmployeeId = employee.Id;
        row.Status = NormalizeParticipantStatus(row.Status);

        if (!AllowedParticipantStatuses.Contains(row.Status, StringComparer.OrdinalIgnoreCase))
        {
            return "Participant status non valido.";
        }

        row.Status = AllowedParticipantStatuses.First(x => string.Equals(x, row.Status, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(row.PhaseCode))
        {
            if (!phasesByTemplate.TryGetValue(cycle.TemplateId, out var phases) ||
                !phases.TryGetValue(row.PhaseCode, out var phase))
            {
                return "PhaseCode non trovato per il template del ciclo.";
            }

            row.CurrentPhaseId = phase.Id;
        }

        return null;
    }

    private void ValidateManagerRelationRows(
        List<ManagerRelationImportRow> rows,
        bool updateExisting,
        Dictionary<string, Employee> activeEmployeeByMatricola,
        Dictionary<string, EmployeeManager> relationByKey,
        PerformanceSetupImportResultDto result,
        List<ManagerRelationImportRow> validRows)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var message = ResolveManagerRelationRow(row, activeEmployeeByMatricola);

            if (message is null)
            {
                var key = RelationKey(row.EmployeeId!.Value, row.ManagerEmployeeId!.Value, row.RelationType);

                if (!seen.Add(key))
                {
                    message = "Manager relation duplicata nel file.";
                }
                else if (!updateExisting && relationByKey.ContainsKey(key))
                {
                    message = "Manager relation gia esistente e update disabilitato.";
                }
            }

            AddRowResult(
                result,
                "ManagerRelations",
                row.RowNumber,
                $"{row.EmployeeMatricola};{row.ManagerMatricola};{row.RelationType}",
                message is null ? (relationByKey.ContainsKey(RelationKey(row.EmployeeId!.Value, row.ManagerEmployeeId!.Value, row.RelationType)) ? "Update" : "Create") : "Error",
                message);

            if (message is null)
            {
                validRows.Add(row);
            }
        }
    }

    private static string? ResolveManagerRelationRow(
        ManagerRelationImportRow row,
        Dictionary<string, Employee> activeEmployeeByMatricola)
    {
        if (string.IsNullOrWhiteSpace(row.EmployeeMatricola))
        {
            return "EmployeeMatricola obbligatoria.";
        }

        if (string.IsNullOrWhiteSpace(row.ManagerMatricola))
        {
            return "ManagerMatricola obbligatoria.";
        }

        if (!activeEmployeeByMatricola.TryGetValue(row.EmployeeMatricola, out var employee))
        {
            return "Employee non trovato o non attivo.";
        }

        if (!activeEmployeeByMatricola.TryGetValue(row.ManagerMatricola, out var manager))
        {
            return "Manager non trovato o non attivo.";
        }

        if (employee.Id == manager.Id)
        {
            return "Employee non puo essere manager di se stesso.";
        }

        row.EmployeeId = employee.Id;
        row.ManagerEmployeeId = manager.Id;
        row.RelationType = NormalizeRelationType(row.RelationType);

        if (!AllowedRelationTypes.Contains(row.RelationType, StringComparer.OrdinalIgnoreCase))
        {
            return "RelationType non valido.";
        }

        row.RelationType = AllowedRelationTypes.First(x => string.Equals(x, row.RelationType, StringComparison.OrdinalIgnoreCase));

        try
        {
            row.StartDate = ParseDateOrNull(row.StartDateText);
            row.EndDate = ParseDateOrNull(row.EndDateText);
        }
        catch (BusinessException)
        {
            return "Formato data non valido. Usa yyyy-MM-dd oppure dd/MM/yyyy.";
        }

        if (row.StartDate.HasValue && row.EndDate.HasValue && row.EndDate.Value < row.StartDate.Value)
        {
            return "EndDate precedente a StartDate.";
        }

        if (row.EndDate.HasValue)
        {
            row.IsPrimary = false;
        }

        return null;
    }

    private static void ValidateGoalAssignmentRows(
        List<GoalAssignmentImportRow> rows,
        bool updateExisting,
        Dictionary<string, Cycle> cycleByName,
        Dictionary<Guid, Cycle> cycleById,
        Dictionary<string, Employee> activeEmployeeByMatricola,
        Dictionary<string, CycleParticipant> participantByKey,
        Dictionary<Guid, Goal> goalById,
        Dictionary<string, Goal> goalByTitle,
        Dictionary<string, GoalAssignment> assignmentByKey,
        PerformanceSetupImportResultDto result,
        List<GoalAssignmentImportRow> validRows)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var weightsByCycleEmployee = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var message = ResolveGoalAssignmentRow(row, cycleByName, cycleById, activeEmployeeByMatricola, participantByKey, goalById, goalByTitle);

            if (message is null)
            {
                var key = GoalAssignmentKey(row.CycleId!.Value, row.EmployeeId!.Value, row.GoalId!.Value);

                if (!seen.Add(key))
                {
                    message = "Goal assignment duplicato nel file.";
                }
                else if (!updateExisting && assignmentByKey.ContainsKey(key))
                {
                    message = "Goal assignment gia esistente e update disabilitato.";
                }
                else
                {
                    var currentKey = GoalAssignmentKey(row.CycleId.Value, row.EmployeeId.Value, row.GoalId.Value);
                    var employeeKey = ParticipantKey(row.CycleId.Value, row.EmployeeId.Value);
                    var existingOtherWeight = assignmentByKey.Values
                        .Where(x => x.CycleId == row.CycleId.Value &&
                                    x.EmployeeId == row.EmployeeId.Value &&
                                    GoalAssignmentKey(x.CycleId, x.EmployeeId, x.GoalId) != currentKey)
                        .Sum(x => x.Weight);
                    weightsByCycleEmployee.TryGetValue(employeeKey, out var currentWeight);
                    currentWeight += row.Weight;
                    weightsByCycleEmployee[employeeKey] = currentWeight;

                    if (existingOtherWeight + currentWeight > 100m)
                    {
                        message = "Totale Weight per employee/ciclo superiore a 100.";
                    }
                }
            }

            AddRowResult(
                result,
                "GoalAssignments",
                row.RowNumber,
                $"{row.Cycle};{row.EmployeeMatricola};{row.Goal}",
                message is null ? (assignmentByKey.ContainsKey(GoalAssignmentKey(row.CycleId!.Value, row.EmployeeId!.Value, row.GoalId!.Value)) ? "Update" : "Create") : "Error",
                message);

            if (message is null)
            {
                validRows.Add(row);
            }
        }
    }

    private static string? ResolveGoalAssignmentRow(
        GoalAssignmentImportRow row,
        Dictionary<string, Cycle> cycleByName,
        Dictionary<Guid, Cycle> cycleById,
        Dictionary<string, Employee> activeEmployeeByMatricola,
        Dictionary<string, CycleParticipant> participantByKey,
        Dictionary<Guid, Goal> goalById,
        Dictionary<string, Goal> goalByTitle)
    {
        if (string.IsNullOrWhiteSpace(row.Cycle) || !TryResolveCycle(row.Cycle, cycleByName, cycleById, out var cycle))
        {
            return "Cycle non trovato.";
        }

        if (!activeEmployeeByMatricola.TryGetValue(row.EmployeeMatricola, out var employee))
        {
            return "Employee non trovato o non attivo.";
        }

        if (!TryResolveGoal(row.Goal, goalById, goalByTitle, out var goal))
        {
            return "Goal non trovato o titolo non univoco.";
        }

        if (!participantByKey.TryGetValue(ParticipantKey(cycle.Id, employee.Id), out var participant) ||
            string.Equals(participant.Status, "Excluded", StringComparison.OrdinalIgnoreCase))
        {
            return "Employee non participant attivo del ciclo.";
        }

        if (!decimal.TryParse(row.WeightText, NumberStyles.Number, CultureInfo.InvariantCulture, out var weight) &&
            !decimal.TryParse(row.WeightText, NumberStyles.Number, CultureInfo.CurrentCulture, out weight))
        {
            return "Weight non valido.";
        }

        if (weight is < 0 or > 100)
        {
            return "Weight fuori range 0-100.";
        }

        try
        {
            row.TargetValue = ParseDecimalOrNull(row.TargetValueText);
            row.StartDate = ParseDateOrNull(row.StartDateText);
            row.DueDate = ParseDateOrNull(row.DueDateText);
        }
        catch (BusinessException)
        {
            return "Formato numero/data non valido.";
        }

        if (row.StartDate.HasValue && row.DueDate.HasValue && row.StartDate.Value > row.DueDate.Value)
        {
            return "StartDate successiva a DueDate.";
        }

        row.Status = NormalizeAssignmentStatus(row.Status);
        if (!AllowedAssignmentStatuses.Contains(row.Status, StringComparer.OrdinalIgnoreCase))
        {
            return "Goal assignment status non valido.";
        }

        row.Status = AllowedAssignmentStatuses.First(x => string.Equals(x, row.Status, StringComparison.OrdinalIgnoreCase));
        row.CycleId = cycle.Id;
        row.EmployeeId = employee.Id;
        row.GoalId = goal.Id;
        row.Weight = weight;
        return null;
    }

    private static void ValidateCompetencyRows(
        List<CompetencyImportRow> rows,
        bool updateExisting,
        Dictionary<string, Competency> competencyByCode,
        PerformanceSetupImportResultDto result,
        List<CompetencyImportRow> validRows)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            row.Code = NormalizeRequiredOrEmpty(row.Code).ToUpperInvariant();
            row.Name = NormalizeRequiredOrEmpty(row.Name);
            row.Description = NormalizeOptionalImportValue(row.Description);
            var message = string.IsNullOrWhiteSpace(row.Code) || string.IsNullOrWhiteSpace(row.Name)
                ? "Code e Name obbligatori."
                : null;

            if (message is null && !seen.Add(row.Code))
            {
                message = "Competency duplicata nel file.";
            }
            else if (message is null && !updateExisting && competencyByCode.ContainsKey(row.Code))
            {
                message = "Competency gia esistente e update disabilitato.";
            }

            AddRowResult(result, "Competencies", row.RowNumber, row.Code, message is null ? (competencyByCode.ContainsKey(row.Code) ? "Update" : "Create") : "Error", message);

            if (message is null)
            {
                validRows.Add(row);
            }
        }
    }

    private static void ValidateCompetencyModelRows(
        List<CompetencyModelImportRow> rows,
        bool updateExisting,
        Dictionary<string, CompetencyModel> modelByName,
        Dictionary<string, Competency> competencyByCode,
        Dictionary<string, CompetencyModelItem> modelItemByKey,
        HashSet<Guid> usedModelIds,
        PerformanceSetupImportResultDto result,
        List<CompetencyModelImportRow> validRows)
    {
        var seenItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var importedItemsByExistingModelId = rows
            .Select(x => new
            {
                ModelName = NormalizeRequiredOrEmpty(x.ModelName),
                CompetencyCode = NormalizeOptionalImportValue(x.CompetencyCode)?.ToUpperInvariant()
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.ModelName) && x.CompetencyCode is not null)
            .Where(x => modelByName.ContainsKey(x.ModelName) && competencyByCode.ContainsKey(x.CompetencyCode!))
            .GroupBy(x => modelByName[x.ModelName].Id)
            .ToDictionary(
                x => x.Key,
                x => x.Select(item => competencyByCode[item.CompetencyCode!].Id).ToHashSet());
        var weightsByModel = modelByName.Values
            .Select(model => new
            {
                model.Name,
                ExistingWeight = modelItemByKey.Values
                    .Where(item => item.ModelId == model.Id)
                    .Where(item => !importedItemsByExistingModelId.TryGetValue(model.Id, out var importedCompetencyIds) ||
                                   !importedCompetencyIds.Contains(item.CompetencyId))
                    .Sum(item => item.Weight ?? 0m)
            })
            .Where(x => x.ExistingWeight > 0m)
            .ToDictionary(x => x.Name, x => x.ExistingWeight, StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var message = ResolveCompetencyModelRow(row, modelByName, competencyByCode, modelItemByKey, usedModelIds, weightsByModel);

            if (message is null)
            {
                if (!updateExisting && row.ModelExists)
                {
                    message = "Competency model gia esistente e update disabilitato.";
                }
                else if (row.CompetencyId.HasValue && !seenItems.Add($"{row.ModelName}|{row.CompetencyCode}"))
                {
                    message = "Competency model item duplicato nel file.";
                }
            }

            AddRowResult(result, "CompetencyModels", row.RowNumber, $"{row.ModelName};{row.CompetencyCode}", message is null ? row.RowStatus : "Error", message);

            if (message is null)
            {
                validRows.Add(row);
            }
        }
    }

    private static string? ResolveCompetencyModelRow(
        CompetencyModelImportRow row,
        Dictionary<string, CompetencyModel> modelByName,
        Dictionary<string, Competency> competencyByCode,
        Dictionary<string, CompetencyModelItem> modelItemByKey,
        HashSet<Guid> usedModelIds,
        Dictionary<string, decimal> weightsByModel)
    {
        row.ModelName = NormalizeRequiredOrEmpty(row.ModelName);
        row.ScaleType = string.IsNullOrWhiteSpace(row.ScaleType) ? "Numeric" : row.ScaleType.Trim();
        row.CompetencyCode = NormalizeOptionalImportValue(row.CompetencyCode)?.ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(row.ModelName))
        {
            return "ModelName obbligatorio.";
        }

        if (!int.TryParse(row.MinScoreText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minScore))
        {
            return "MinScore non valido.";
        }

        if (!int.TryParse(row.MaxScoreText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxScore))
        {
            return "MaxScore non valido.";
        }

        if (minScore > maxScore)
        {
            return "MinScore maggiore di MaxScore.";
        }

        row.MinScore = minScore;
        row.MaxScore = maxScore;
        row.ModelExists = modelByName.TryGetValue(row.ModelName, out var model);
        row.ModelId = model?.Id;

        if (model is not null && usedModelIds.Contains(model.Id))
        {
            return "Competency model usato da assessment: struttura non modificabile.";
        }

        if (row.CompetencyCode is null)
        {
            row.RowStatus = row.ModelExists ? "Update" : "Create";
            return null;
        }

        if (!competencyByCode.TryGetValue(row.CompetencyCode, out var competency) || !competency.IsActive)
        {
            return "CompetencyCode non trovato o non attivo.";
        }

        row.CompetencyId = competency.Id;
        try
        {
            row.Weight = ParseDecimalOrNull(row.WeightText);
        }
        catch (BusinessException)
        {
            return "Weight non valido.";
        }

        if (row.Weight is < 0 or > 100)
        {
            return "Weight fuori range 0-100.";
        }

        weightsByModel.TryGetValue(row.ModelName, out var currentWeight);
        currentWeight += row.Weight ?? 0m;
        weightsByModel[row.ModelName] = currentWeight;

        if (currentWeight > 100m)
        {
            return "Totale Weight del model superiore a 100.";
        }

        row.IsRequired = ParseBool(row.IsRequiredText);
        row.RowStatus = row.ModelExists &&
                        model is not null &&
                        modelItemByKey.ContainsKey(CompetencyModelItemKey(model.Id, competency.Id))
            ? "Update"
            : "Create";
        return null;
    }

    private async Task ApplyOrgUnitRowsAsync(
        List<OrgUnitImportRow> rows,
        Dictionary<string, OrgUnit> orgUnitByName,
        PerformanceSetupImportResultDto result)
    {
        foreach (var row in rows)
        {
            var isUpdate = orgUnitByName.TryGetValue(row.Name, out var entity);

            entity ??= new OrgUnit
            {
                TenantId = CurrentTenant.Id,
                Name = row.Name
            };

            if (isUpdate)
            {
                await _orgUnitRepository.UpdateAsync(entity, autoSave: false);
                result.UpdatedOrgUnits++;
            }
            else
            {
                await _orgUnitRepository.InsertAsync(entity, autoSave: false);
                orgUnitByName[row.Name] = entity;
                result.CreatedOrgUnits++;
            }
        }

        foreach (var row in rows)
        {
            var entity = orgUnitByName[row.Name];
            entity.ParentOrgUnitId = row.ParentName is not null && orgUnitByName.TryGetValue(row.ParentName, out var parent)
                ? parent.Id
                : null;
            await _orgUnitRepository.UpdateAsync(entity, autoSave: false);
        }
    }

    private async Task ApplyJobRoleRowsAsync(
        List<JobRoleImportRow> rows,
        Dictionary<string, JobRole> jobRoleByName,
        PerformanceSetupImportResultDto result)
    {
        foreach (var row in rows)
        {
            var isUpdate = jobRoleByName.TryGetValue(row.Name, out var entity);

            entity ??= new JobRole
            {
                TenantId = CurrentTenant.Id,
                Name = row.Name
            };

            if (isUpdate)
            {
                await _jobRoleRepository.UpdateAsync(entity, autoSave: false);
                result.UpdatedJobRoles++;
            }
            else
            {
                await _jobRoleRepository.InsertAsync(entity, autoSave: false);
                jobRoleByName[row.Name] = entity;
                result.CreatedJobRoles++;
            }
        }
    }

    private async Task ApplyCompetencyRowsAsync(
        List<CompetencyImportRow> rows,
        Dictionary<string, Competency> competencyByCode,
        PerformanceSetupImportResultDto result)
    {
        foreach (var row in rows)
        {
            var isUpdate = competencyByCode.TryGetValue(row.Code, out var entity);

            entity ??= new Competency
            {
                TenantId = CurrentTenant.Id,
                Code = row.Code
            };

            entity.Name = row.Name;
            entity.Description = row.Description;
            entity.IsActive = row.IsActive;

            if (isUpdate)
            {
                await _competencyRepository.UpdateAsync(entity, autoSave: false);
                result.UpdatedCompetencies++;
            }
            else
            {
                await _competencyRepository.InsertAsync(entity, autoSave: false);
                competencyByCode[row.Code] = entity;
                result.CreatedCompetencies++;
            }
        }
    }

    private async Task ApplyCompetencyModelRowsAsync(
        List<CompetencyModelImportRow> rows,
        Dictionary<string, CompetencyModel> modelByName,
        Dictionary<string, Competency> competencyByCode,
        Dictionary<string, CompetencyModelItem> modelItemByKey,
        PerformanceSetupImportResultDto result)
    {
        foreach (var modelGroup in rows.GroupBy(x => x.ModelName, StringComparer.OrdinalIgnoreCase))
        {
            var first = modelGroup.First();
            var isUpdate = modelByName.TryGetValue(first.ModelName, out var model);

            model ??= new CompetencyModel
            {
                TenantId = CurrentTenant.Id,
                Name = first.ModelName
            };

            model.ScaleType = first.ScaleType;
            model.MinScore = first.MinScore;
            model.MaxScore = first.MaxScore;

            if (isUpdate)
            {
                await _competencyModelRepository.UpdateAsync(model, autoSave: false);
                result.UpdatedCompetencyModels++;
            }
            else
            {
                await _competencyModelRepository.InsertAsync(model, autoSave: false);
                modelByName[first.ModelName] = model;
                result.CreatedCompetencyModels++;
            }

            foreach (var row in modelGroup.Where(x => x.CompetencyCode is not null))
            {
                var competency = competencyByCode[row.CompetencyCode!];
                var key = CompetencyModelItemKey(model.Id, competency.Id);
                var isItemUpdate = modelItemByKey.TryGetValue(key, out var item);

                item ??= new CompetencyModelItem
                {
                    TenantId = CurrentTenant.Id,
                    ModelId = model.Id,
                    CompetencyId = competency.Id
                };

                item.Weight = row.Weight;
                item.IsRequired = row.IsRequired;

                if (isItemUpdate)
                {
                    await _competencyModelItemRepository.UpdateAsync(item, autoSave: false);
                    result.UpdatedCompetencyModelItems++;
                }
                else
                {
                    await _competencyModelItemRepository.InsertAsync(item, autoSave: false);
                    modelItemByKey[key] = item;
                    result.CreatedCompetencyModelItems++;
                }
            }
        }
    }

    private async Task ApplyParticipantRowsAsync(
        List<ParticipantImportRow> rows,
        Dictionary<string, CycleParticipant> participantByKey,
        PerformanceSetupImportResultDto result)
    {
        foreach (var row in rows)
        {
            var key = ParticipantKey(row.CycleId!.Value, row.EmployeeId!.Value);
            var isUpdate = participantByKey.TryGetValue(key, out var entity);

            entity ??= new CycleParticipant
            {
                TenantId = CurrentTenant.Id,
                CycleId = row.CycleId.Value,
                EmployeeId = row.EmployeeId.Value
            };

            entity.CurrentPhaseId = row.CurrentPhaseId;
            entity.Status = row.Status;

            if (isUpdate)
            {
                await _participantRepository.UpdateAsync(entity, autoSave: false);
                result.UpdatedParticipants++;
            }
            else
            {
                await _participantRepository.InsertAsync(entity, autoSave: false);
                participantByKey[key] = entity;
                result.CreatedParticipants++;
            }
        }
    }

    private async Task ApplyManagerRelationRowsAsync(
        List<ManagerRelationImportRow> rows,
        Dictionary<string, EmployeeManager> relationByKey,
        PerformanceSetupImportResultDto result)
    {
        foreach (var row in rows)
        {
            var key = RelationKey(row.EmployeeId!.Value, row.ManagerEmployeeId!.Value, row.RelationType);
            var isUpdate = relationByKey.TryGetValue(key, out var entity);

            entity ??= new EmployeeManager
            {
                TenantId = CurrentTenant.Id,
                EmployeeId = row.EmployeeId.Value,
                ManagerEmployeeId = row.ManagerEmployeeId.Value
            };

            entity.RelationType = row.RelationType;
            entity.IsPrimary = row.IsPrimary && !row.EndDate.HasValue;
            entity.StartDate = row.StartDate;
            entity.EndDate = row.EndDate;

            if (entity.IsPrimary)
            {
                await ClearOtherPrimaryRelationsAsync(entity, relationByKey.Values);
            }

            if (isUpdate)
            {
                await _managerRelationRepository.UpdateAsync(entity, autoSave: false);
                result.UpdatedManagerRelations++;
            }
            else
            {
                await _managerRelationRepository.InsertAsync(entity, autoSave: false);
                relationByKey[key] = entity;
                result.CreatedManagerRelations++;
            }
        }
    }

    private async Task ApplyGoalAssignmentRowsAsync(
        List<GoalAssignmentImportRow> rows,
        Dictionary<string, GoalAssignment> assignmentByKey,
        PerformanceSetupImportResultDto result)
    {
        foreach (var row in rows)
        {
            var key = GoalAssignmentKey(row.CycleId!.Value, row.EmployeeId!.Value, row.GoalId!.Value);
            var isUpdate = assignmentByKey.TryGetValue(key, out var entity);

            entity ??= new GoalAssignment
            {
                TenantId = CurrentTenant.Id,
                CycleId = row.CycleId.Value,
                EmployeeId = row.EmployeeId.Value,
                GoalId = row.GoalId.Value
            };

            entity.Weight = row.Weight;
            entity.TargetValue = row.TargetValue;
            entity.StartDate = row.StartDate;
            entity.DueDate = row.DueDate;
            entity.Status = row.Status;

            if (isUpdate)
            {
                await _goalAssignmentRepository.UpdateAsync(entity, autoSave: false);
                result.UpdatedGoalAssignments++;
            }
            else
            {
                await _goalAssignmentRepository.InsertAsync(entity, autoSave: false);
                assignmentByKey[key] = entity;
                result.CreatedGoalAssignments++;
            }
        }
    }

    private async Task ClearOtherPrimaryRelationsAsync(EmployeeManager entity, IEnumerable<EmployeeManager> relations)
    {
        var today = DateOnly.FromDateTime(Clock.Now);
        var otherPrimaries = relations.Where(x =>
            x.EmployeeId == entity.EmployeeId &&
            x.RelationType == entity.RelationType &&
            x.IsPrimary &&
            x.Id != entity.Id &&
            (!x.StartDate.HasValue || x.StartDate.Value <= today) &&
            (!x.EndDate.HasValue || x.EndDate.Value >= today));

        foreach (var relation in otherPrimaries)
        {
            relation.IsPrimary = false;
            await _managerRelationRepository.UpdateAsync(relation, autoSave: false);
        }
    }

    private static List<ParticipantImportRow> ParseParticipantRows(string? content)
    {
        return ParseLines(content)
            .Where(x => !IsHeader(x.Columns, "Cycle"))
            .Select(x => new ParticipantImportRow
            {
                RowNumber = x.RowNumber,
                Cycle = GetColumn(x.Columns, 0),
                EmployeeMatricola = GetColumn(x.Columns, 1),
                PhaseCode = NormalizeOptionalImportValue(GetColumn(x.Columns, 2)),
                Status = GetColumn(x.Columns, 3)
            })
            .ToList();
    }

    private static List<OrgUnitImportRow> ParseOrgUnitRows(string? content)
    {
        return ParseLines(content)
            .Where(x => !IsHeader(x.Columns, "Name"))
            .Select(x => new OrgUnitImportRow
            {
                RowNumber = x.RowNumber,
                Name = GetColumn(x.Columns, 0),
                ParentName = GetColumn(x.Columns, 1)
            })
            .ToList();
    }

    private static List<JobRoleImportRow> ParseJobRoleRows(string? content)
    {
        return ParseLines(content)
            .Where(x => !IsHeader(x.Columns, "Name"))
            .Select(x => new JobRoleImportRow
            {
                RowNumber = x.RowNumber,
                Name = GetColumn(x.Columns, 0)
            })
            .ToList();
    }

    private static List<ManagerRelationImportRow> ParseManagerRelationRows(string? content)
    {
        return ParseLines(content)
            .Where(x => !IsHeader(x.Columns, "EmployeeMatricola"))
            .Select(x => new ManagerRelationImportRow
            {
                RowNumber = x.RowNumber,
                EmployeeMatricola = GetColumn(x.Columns, 0),
                ManagerMatricola = GetColumn(x.Columns, 1),
                RelationType = GetColumn(x.Columns, 2),
                IsPrimary = ParseBool(GetColumn(x.Columns, 3)),
                StartDateText = GetColumn(x.Columns, 4),
                EndDateText = GetColumn(x.Columns, 5)
            })
            .ToList();
    }

    private static List<GoalAssignmentImportRow> ParseGoalAssignmentRows(string? content)
    {
        return ParseLines(content)
            .Where(x => !IsHeader(x.Columns, "Cycle"))
            .Select(x => new GoalAssignmentImportRow
            {
                RowNumber = x.RowNumber,
                Cycle = GetColumn(x.Columns, 0),
                EmployeeMatricola = GetColumn(x.Columns, 1),
                Goal = GetColumn(x.Columns, 2),
                WeightText = GetColumn(x.Columns, 3),
                TargetValueText = GetColumn(x.Columns, 4),
                StartDateText = GetColumn(x.Columns, 5),
                DueDateText = GetColumn(x.Columns, 6),
                Status = GetColumn(x.Columns, 7)
            })
            .ToList();
    }

    private static List<CompetencyImportRow> ParseCompetencyRows(string? content)
    {
        return ParseLines(content)
            .Where(x => !IsHeader(x.Columns, "Code"))
            .Select(x => new CompetencyImportRow
            {
                RowNumber = x.RowNumber,
                Code = GetColumn(x.Columns, 0),
                Name = GetColumn(x.Columns, 1),
                Description = GetColumn(x.Columns, 2),
                IsActive = ParseBool(GetColumn(x.Columns, 3))
            })
            .ToList();
    }

    private static List<CompetencyModelImportRow> ParseCompetencyModelRows(string? content)
    {
        return ParseLines(content)
            .Where(x => !IsHeader(x.Columns, "ModelName"))
            .Select(x => new CompetencyModelImportRow
            {
                RowNumber = x.RowNumber,
                ModelName = GetColumn(x.Columns, 0),
                ScaleType = GetColumn(x.Columns, 1),
                MinScoreText = GetColumn(x.Columns, 2),
                MaxScoreText = GetColumn(x.Columns, 3),
                CompetencyCode = GetColumn(x.Columns, 4),
                WeightText = GetColumn(x.Columns, 5),
                IsRequiredText = GetColumn(x.Columns, 6)
            })
            .ToList();
    }

    private static List<ParsedImportLine> ParseLines(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        return content
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n')
            .Select((line, index) => new { Line = line.Trim(), RowNumber = index + 1 })
            .Where(x => !string.IsNullOrWhiteSpace(x.Line))
            .Select(x => new ParsedImportLine
            {
                RowNumber = x.RowNumber,
                Columns = SplitImportLine(x.Line)
            })
            .ToList();
    }

    private static string[] SplitImportLine(string line)
    {
        var separator = line.Contains(';') ? ';' : ',';
        return line.Split(separator).Select(x => x.Trim()).ToArray();
    }

    private static bool IsHeader(string[] columns, string firstColumnName)
        => columns.Length > 0 && string.Equals(columns[0], firstColumnName, StringComparison.OrdinalIgnoreCase);

    private static string GetColumn(string[] columns, int index)
        => columns.Length > index ? columns[index].Trim() : string.Empty;

    private static string? NormalizeOptionalImportValue(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return normalized is null || normalized == "-" ? null : normalized;
    }

    private static string NormalizeRequiredOrEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string NormalizeParticipantStatus(string? value)
        => string.IsNullOrWhiteSpace(value) ? "Active" : value.Trim();

    private static string NormalizeRelationType(string? value)
        => string.IsNullOrWhiteSpace(value) ? "Line" : value.Trim();

    private static string NormalizeAssignmentStatus(string? value)
        => string.IsNullOrWhiteSpace(value) ? "Draft" : value.Trim();

    private static bool ParseBool(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var normalized = value.Trim();

        return normalized.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("1", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("si", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private static DateOnly? ParseDateOrNull(string? value)
    {
        var normalized = NormalizeOptionalImportValue(value);

        if (normalized is null)
        {
            return null;
        }

        var formats = new[] { "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy" };

        if (DateOnly.TryParseExact(normalized, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ||
            DateOnly.TryParse(normalized, CultureInfo.CurrentCulture, DateTimeStyles.None, out parsed))
        {
            return parsed;
        }

        throw new BusinessException("ImportDateInvalidFormat").WithData("Value", normalized);
    }

    private static decimal? ParseDecimalOrNull(string? value)
    {
        var normalized = NormalizeOptionalImportValue(value);

        if (normalized is null)
        {
            return null;
        }

        if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ||
            decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.CurrentCulture, out parsed))
        {
            return parsed;
        }

        throw new BusinessException("ImportDecimalInvalidFormat").WithData("Value", normalized);
    }

    private static bool TryResolveCycle(
        string value,
        Dictionary<string, Cycle> cycleByName,
        Dictionary<Guid, Cycle> cycleById,
        out Cycle cycle)
    {
        if (Guid.TryParse(value, out var cycleId) && cycleById.TryGetValue(cycleId, out cycle!))
        {
            return true;
        }

        return cycleByName.TryGetValue(value, out cycle!);
    }

    private static bool TryResolveGoal(
        string value,
        Dictionary<Guid, Goal> goalById,
        Dictionary<string, Goal> goalByTitle,
        out Goal goal)
    {
        if (Guid.TryParse(value, out var goalId) && goalById.TryGetValue(goalId, out goal!))
        {
            return true;
        }

        return goalByTitle.TryGetValue(value, out goal!);
    }

    private static string ParticipantKey(Guid cycleId, Guid employeeId)
        => $"{cycleId:N}|{employeeId:N}";

    private static string RelationKey(Guid employeeId, Guid managerEmployeeId, string relationType)
        => $"{employeeId:N}|{managerEmployeeId:N}|{relationType.ToUpperInvariant()}";

    private static string GoalAssignmentKey(Guid cycleId, Guid employeeId, Guid goalId)
        => $"{cycleId:N}|{employeeId:N}|{goalId:N}";

    private static string CompetencyModelItemKey(Guid modelId, Guid competencyId)
        => $"{modelId:N}|{competencyId:N}";

    private static void AddRowResult(
        PerformanceSetupImportResultDto result,
        string section,
        int rowNumber,
        string key,
        string status,
        string? message = null)
    {
        result.Rows.Add(new PerformanceSetupImportRowResultDto
        {
            Section = section,
            RowNumber = rowNumber,
            Key = key,
            Status = status,
            Message = message
        });
    }

    private class OrgUnitImportRow
    {
        public int RowNumber { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ParentName { get; set; }
    }

    private class JobRoleImportRow
    {
        public int RowNumber { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private class ParsedImportLine
    {
        public int RowNumber { get; set; }
        public string[] Columns { get; set; } = [];
    }

    private class ParticipantImportRow
    {
        public int RowNumber { get; set; }
        public string Cycle { get; set; } = string.Empty;
        public string EmployeeMatricola { get; set; } = string.Empty;
        public string? PhaseCode { get; set; }
        public string Status { get; set; } = "Active";
        public Guid? CycleId { get; set; }
        public Guid? EmployeeId { get; set; }
        public Guid? CurrentPhaseId { get; set; }
    }

    private class ManagerRelationImportRow
    {
        public int RowNumber { get; set; }
        public string EmployeeMatricola { get; set; } = string.Empty;
        public string ManagerMatricola { get; set; } = string.Empty;
        public string RelationType { get; set; } = "Line";
        public bool IsPrimary { get; set; } = true;
        public string? StartDateText { get; set; }
        public string? EndDateText { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public Guid? EmployeeId { get; set; }
        public Guid? ManagerEmployeeId { get; set; }
    }

    private class GoalAssignmentImportRow
    {
        public int RowNumber { get; set; }
        public string Cycle { get; set; } = string.Empty;
        public string EmployeeMatricola { get; set; } = string.Empty;
        public string Goal { get; set; } = string.Empty;
        public string WeightText { get; set; } = string.Empty;
        public string? TargetValueText { get; set; }
        public string? StartDateText { get; set; }
        public string? DueDateText { get; set; }
        public string Status { get; set; } = "Draft";
        public Guid? CycleId { get; set; }
        public Guid? EmployeeId { get; set; }
        public Guid? GoalId { get; set; }
        public decimal Weight { get; set; }
        public decimal? TargetValue { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? DueDate { get; set; }
    }

    private class CompetencyImportRow
    {
        public int RowNumber { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
    }

    private class CompetencyModelImportRow
    {
        public int RowNumber { get; set; }
        public string ModelName { get; set; } = string.Empty;
        public string ScaleType { get; set; } = "Numeric";
        public string MinScoreText { get; set; } = string.Empty;
        public string MaxScoreText { get; set; } = string.Empty;
        public string? CompetencyCode { get; set; }
        public string? WeightText { get; set; }
        public string? IsRequiredText { get; set; }
        public int MinScore { get; set; }
        public int MaxScore { get; set; }
        public Guid? ModelId { get; set; }
        public Guid? CompetencyId { get; set; }
        public decimal? Weight { get; set; }
        public bool IsRequired { get; set; }
        public bool ModelExists { get; set; }
        public string RowStatus { get; set; } = "Create";
    }
}
