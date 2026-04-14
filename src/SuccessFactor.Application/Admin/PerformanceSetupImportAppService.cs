using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SuccessFactor.Cycles;
using SuccessFactor.Employees;
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

    private readonly ICurrentUser _currentUser;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IRepository<Cycle, Guid> _cycleRepository;
    private readonly IRepository<CycleParticipant, Guid> _participantRepository;
    private readonly IRepository<Employee, Guid> _employeeRepository;
    private readonly IRepository<EmployeeManager, Guid> _managerRelationRepository;
    private readonly IRepository<ProcessPhase, Guid> _phaseRepository;

    public PerformanceSetupImportAppService(
        ICurrentUser currentUser,
        IAsyncQueryableExecuter asyncExecuter,
        IRepository<Cycle, Guid> cycleRepository,
        IRepository<CycleParticipant, Guid> participantRepository,
        IRepository<Employee, Guid> employeeRepository,
        IRepository<EmployeeManager, Guid> managerRelationRepository,
        IRepository<ProcessPhase, Guid> phaseRepository)
    {
        _currentUser = currentUser;
        _asyncExecuter = asyncExecuter;
        _cycleRepository = cycleRepository;
        _participantRepository = participantRepository;
        _employeeRepository = employeeRepository;
        _managerRelationRepository = managerRelationRepository;
        _phaseRepository = phaseRepository;
    }

    public async Task<PerformanceSetupImportResultDto> ImportAsync(ImportPerformanceSetupInput input)
    {
        EnsureTenantAndAdmin();

        if (input is null ||
            (string.IsNullOrWhiteSpace(input.ParticipantsContent) && string.IsNullOrWhiteSpace(input.ManagerRelationsContent)))
        {
            throw new BusinessException("PerformanceSetupImportContentRequired");
        }

        var cycleQuery = await _cycleRepository.GetQueryableAsync();
        var cycles = await _asyncExecuter.ToListAsync(cycleQuery);
        var cycleByName = cycles.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var cycleById = cycles.ToDictionary(x => x.Id);

        var employeeQuery = await _employeeRepository.GetQueryableAsync();
        var employees = await _asyncExecuter.ToListAsync(employeeQuery);
        var activeEmployeeByMatricola = employees
            .Where(x => x.IsActive)
            .ToDictionary(x => x.Matricola, StringComparer.OrdinalIgnoreCase);

        var participantQuery = await _participantRepository.GetQueryableAsync();
        var existingParticipants = await _asyncExecuter.ToListAsync(participantQuery);
        var participantByKey = existingParticipants.ToDictionary(x => ParticipantKey(x.CycleId, x.EmployeeId));

        var relationQuery = await _managerRelationRepository.GetQueryableAsync();
        var existingRelations = await _asyncExecuter.ToListAsync(relationQuery);
        var relationByKey = existingRelations.ToDictionary(x => RelationKey(x.EmployeeId, x.ManagerEmployeeId, x.RelationType));

        var phaseQuery = await _phaseRepository.GetQueryableAsync();
        var phases = await _asyncExecuter.ToListAsync(phaseQuery);
        var phasesByTemplate = phases
            .GroupBy(x => x.TemplateId)
            .ToDictionary(
                x => x.Key,
                x => x.ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase));

        var participantRows = ParseParticipantRows(input.ParticipantsContent);
        var relationRows = ParseManagerRelationRows(input.ManagerRelationsContent);
        var result = new PerformanceSetupImportResultDto();
        var validParticipantRows = new List<ParticipantImportRow>();
        var validRelationRows = new List<ManagerRelationImportRow>();

        ValidateParticipantRows(
            participantRows,
            input.UpdateExisting,
            cycleByName,
            cycleById,
            activeEmployeeByMatricola,
            phasesByTemplate,
            participantByKey,
            result,
            validParticipantRows);

        ValidateManagerRelationRows(
            relationRows,
            input.UpdateExisting,
            activeEmployeeByMatricola,
            relationByKey,
            result,
            validRelationRows);

        result.ErrorCount = result.Rows.Count(x => x.Status == "Error");
        result.HasErrors = result.ErrorCount > 0;

        if (result.HasErrors)
        {
            return result;
        }

        foreach (var row in validParticipantRows)
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

        foreach (var row in validRelationRows)
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

        if (CurrentUnitOfWork is null)
        {
            throw new BusinessException("UnitOfWorkMissing");
        }

        await CurrentUnitOfWork.SaveChangesAsync();
        return result;
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

    private static string NormalizeParticipantStatus(string? value)
        => string.IsNullOrWhiteSpace(value) ? "Active" : value.Trim();

    private static string NormalizeRelationType(string? value)
        => string.IsNullOrWhiteSpace(value) ? "Line" : value.Trim();

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

    private static string ParticipantKey(Guid cycleId, Guid employeeId)
        => $"{cycleId:N}|{employeeId:N}";

    private static string RelationKey(Guid employeeId, Guid managerEmployeeId, string relationType)
        => $"{employeeId:N}|{managerEmployeeId:N}|{relationType.ToUpperInvariant()}";

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
}
