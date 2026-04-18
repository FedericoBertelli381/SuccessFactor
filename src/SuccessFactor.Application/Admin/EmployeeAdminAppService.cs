using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SuccessFactor.Employees;
using SuccessFactor.Security;
using SuccessFactor.JobRoles;
using SuccessFactor.OrgUnits;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Users;

namespace SuccessFactor.Admin;

[Authorize]
public class EmployeeAdminAppService : ApplicationService, IEmployeeAdminAppService
{
    private readonly ICurrentUser _currentUser;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IRepository<Employee, Guid> _employeeRepository;
    private readonly IRepository<OrgUnit, Guid> _orgUnitRepository;
    private readonly IRepository<JobRole, Guid> _jobRoleRepository;

    public EmployeeAdminAppService(
        ICurrentUser currentUser,
        IAsyncQueryableExecuter asyncExecuter,
        IRepository<Employee, Guid> employeeRepository,
        IRepository<OrgUnit, Guid> orgUnitRepository,
        IRepository<JobRole, Guid> jobRoleRepository)
    {
        _currentUser = currentUser;
        _asyncExecuter = asyncExecuter;
        _employeeRepository = employeeRepository;
        _orgUnitRepository = orgUnitRepository;
        _jobRoleRepository = jobRoleRepository;
    }

    public async Task<EmployeeAdminDto> GetAsync()
    {
        EnsureTenantAndAdmin();

        var employeeQuery = await _employeeRepository.GetQueryableAsync();
        var employees = await _asyncExecuter.ToListAsync(
            employeeQuery
                .OrderBy(x => x.Matricola)
                .ThenBy(x => x.FullName));

        var orgUnitQuery = await _orgUnitRepository.GetQueryableAsync();
        var orgUnits = await _asyncExecuter.ToListAsync(
            orgUnitQuery.OrderBy(x => x.Name));

        var jobRoleQuery = await _jobRoleRepository.GetQueryableAsync();
        var jobRoles = await _asyncExecuter.ToListAsync(
            jobRoleQuery.OrderBy(x => x.Name));

        var orgUnitById = orgUnits.ToDictionary(x => x.Id, x => x.Name);
        var jobRoleById = jobRoles.ToDictionary(x => x.Id, x => x.Name);

        return new EmployeeAdminDto
        {
            Employees = employees.Select(x => new EmployeeAdminListItemDto
            {
                EmployeeId = x.Id,
                UserId = x.UserId,
                Matricola = x.Matricola,
                FullName = x.FullName,
                Email = x.Email,
                OrgUnitId = x.OrgUnitId,
                OrgUnitName = x.OrgUnitId.HasValue && orgUnitById.TryGetValue(x.OrgUnitId.Value, out var orgUnitName) ? orgUnitName : null,
                JobRoleId = x.JobRoleId,
                JobRoleName = x.JobRoleId.HasValue && jobRoleById.TryGetValue(x.JobRoleId.Value, out var jobRoleName) ? jobRoleName : null,
                IsActive = x.IsActive
            }).ToList(),
            OrgUnits = orgUnits.Select(x => new AdminLookupDto
            {
                Id = x.Id,
                Name = x.Name
            }).ToList(),
            JobRoles = jobRoles.Select(x => new AdminLookupDto
            {
                Id = x.Id,
                Name = x.Name
            }).ToList(),
            NewEmployeeDefaults = new CreateUpdateEmployeeDto
            {
                IsActive = true
            }
        };
    }

    public async Task<EmployeeAdminListItemDto> SaveAsync(Guid? id, CreateUpdateEmployeeDto input)
    {
        EnsureTenantAndAdmin();
        input.Matricola = NormalizeRequired(input.Matricola, "Matricola");
        input.FullName = NormalizeRequired(input.FullName, "FullName");
        input.Email = NormalizeNullable(input.Email);
        input.OrgUnitId = NormalizeGuidString(input.OrgUnitId, "OrgUnitIdInvalidFormat");
        input.JobRoleId = NormalizeGuidString(input.JobRoleId, "JobRoleIdInvalidFormat");

        await ValidateReferencesAsync(input);
        await EnsureNoDuplicateMatricolaAsync(id, input.Matricola);

        Employee entity;

        if (id.HasValue)
        {
            entity = await _employeeRepository.GetAsync(id.Value);
        }
        else
        {
            entity = new Employee
            {
                TenantId = CurrentTenant.Id
            };
        }

        entity.Matricola = input.Matricola;
        entity.FullName = input.FullName;
        entity.Email = input.Email;
        entity.OrgUnitId = ParseGuidOrNull(input.OrgUnitId);
        entity.JobRoleId = ParseGuidOrNull(input.JobRoleId);
        entity.IsActive = input.IsActive;

        if (id.HasValue)
        {
            entity = await _employeeRepository.UpdateAsync(entity, autoSave: true);
        }
        else
        {
            entity = await _employeeRepository.InsertAsync(entity, autoSave: true);
        }

        var orgUnitName = await ResolveOrgUnitNameAsync(entity.OrgUnitId);
        var jobRoleName = await ResolveJobRoleNameAsync(entity.JobRoleId);

        return new EmployeeAdminListItemDto
        {
            EmployeeId = entity.Id,
            UserId = entity.UserId,
            Matricola = entity.Matricola,
            FullName = entity.FullName,
            Email = entity.Email,
            OrgUnitId = entity.OrgUnitId,
            OrgUnitName = orgUnitName,
            JobRoleId = entity.JobRoleId,
            JobRoleName = jobRoleName,
            IsActive = entity.IsActive
        };
    }

    public async Task<EmployeeImportResultDto> ImportAsync(ImportEmployeesInput input)
    {
        EnsureTenantAndAdmin();

        if (input is null || string.IsNullOrWhiteSpace(input.Content))
        {
            throw new BusinessException("EmployeeImportContentRequired");
        }

        var employeeQuery = await _employeeRepository.GetQueryableAsync();
        var existingEmployees = await _asyncExecuter.ToListAsync(employeeQuery);
        var existingByMatricola = existingEmployees.ToDictionary(x => x.Matricola, StringComparer.OrdinalIgnoreCase);

        var orgUnitQuery = await _orgUnitRepository.GetQueryableAsync();
        var orgUnits = await _asyncExecuter.ToListAsync(orgUnitQuery);
        var orgUnitByName = orgUnits.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        var jobRoleQuery = await _jobRoleRepository.GetQueryableAsync();
        var jobRoles = await _asyncExecuter.ToListAsync(jobRoleQuery);
        var jobRoleByName = jobRoles.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        var rows = ParseImportRows(input.Content);
        var result = new EmployeeImportResultDto();
        var seenMatricole = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var validRows = new List<EmployeeImportRow>();

        foreach (var row in rows)
        {
            var validationMessage = ValidateImportRow(row, input.UpdateExisting, existingByMatricola, orgUnitByName, jobRoleByName, seenMatricole);

            if (!string.IsNullOrWhiteSpace(validationMessage))
            {
                result.Rows.Add(new EmployeeImportRowResultDto
                {
                    RowNumber = row.RowNumber,
                    Matricola = row.Matricola,
                    FullName = row.FullName,
                    Status = "Error",
                    Message = validationMessage
                });
                continue;
            }

            validRows.Add(row);
            result.Rows.Add(new EmployeeImportRowResultDto
            {
                RowNumber = row.RowNumber,
                Matricola = row.Matricola,
                FullName = row.FullName,
                Status = existingByMatricola.ContainsKey(row.Matricola) ? "Update" : "Create"
            });
        }

        result.ErrorCount = result.Rows.Count(x => x.Status == "Error");
        result.HasErrors = result.ErrorCount > 0;

        if (result.HasErrors)
        {
            return result;
        }

        foreach (var row in validRows)
        {
            var isUpdate = existingByMatricola.TryGetValue(row.Matricola, out var entity);

            entity ??= new Employee
            {
                TenantId = CurrentTenant.Id
            };
            entity.Matricola = row.Matricola;
            entity.FullName = row.FullName;
            entity.Email = row.Email;
            entity.OrgUnitId = ResolveOrgUnitId(row.OrgUnitName, orgUnitByName);
            entity.JobRoleId = ResolveJobRoleId(row.JobRoleName, jobRoleByName);
            entity.IsActive = row.IsActive;

            if (isUpdate)
            {
                await _employeeRepository.UpdateAsync(entity, autoSave: false);
                result.UpdatedCount++;
            }
            else
            {
                await _employeeRepository.InsertAsync(entity, autoSave: false);
                result.CreatedCount++;
            }
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

        if (!SuccessFactorRoles.IsAdmin(roles))
        {
            throw new BusinessException("CurrentUserIsNotAdmin");
        }
    }

    private async Task ValidateReferencesAsync(CreateUpdateEmployeeDto input)
    {
        var orgUnitId = ParseGuidOrNull(input.OrgUnitId);
        var jobRoleId = ParseGuidOrNull(input.JobRoleId);

        if (orgUnitId.HasValue && !await _orgUnitRepository.AnyAsync(x => x.Id == orgUnitId.Value))
        {
            throw new BusinessException("OrgUnitNotFound");
        }

        if (jobRoleId.HasValue && !await _jobRoleRepository.AnyAsync(x => x.Id == jobRoleId.Value))
        {
            throw new BusinessException("JobRoleNotFound");
        }
    }

    private async Task EnsureNoDuplicateMatricolaAsync(Guid? excludeId, string matricola)
    {
        if (await _employeeRepository.AnyAsync(x =>
            x.Matricola == matricola &&
            (!excludeId.HasValue || x.Id != excludeId.Value)))
        {
            throw new BusinessException("EmployeeMatricolaAlreadyExists");
        }
    }

    private async Task<string?> ResolveOrgUnitNameAsync(Guid? orgUnitId)
    {
        if (!orgUnitId.HasValue)
        {
            return null;
        }

        var entity = await _orgUnitRepository.FindAsync(orgUnitId.Value);
        return entity?.Name;
    }

    private async Task<string?> ResolveJobRoleNameAsync(Guid? jobRoleId)
    {
        if (!jobRoleId.HasValue)
        {
            return null;
        }

        var entity = await _jobRoleRepository.FindAsync(jobRoleId.Value);
        return entity?.Name;
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

    private static string? NormalizeGuidString(string? value, string errorCode)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!Guid.TryParse(value, out _))
        {
            throw new BusinessException(errorCode);
        }

        return value.Trim();
    }

    private static Guid? ParseGuidOrNull(string? value)
        => Guid.TryParse(value, out var parsed) ? parsed : null;

    private static string? NormalizeOptionalImportValue(string? value)
    {
        var normalized = NormalizeNullable(value);

        if (normalized is null || normalized == "-")
        {
            return null;
        }

        return normalized;
    }

    private static List<EmployeeImportRow> ParseImportRows(string content)
    {
        var result = new List<EmployeeImportRow>();
        var lines = content
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n')
            .Select((line, index) => new { Line = line.Trim(), RowNumber = index + 1 })
            .Where(x => !string.IsNullOrWhiteSpace(x.Line))
            .ToList();

        foreach (var line in lines)
        {
            var columns = SplitImportLine(line.Line);

            if (IsHeader(columns))
            {
                continue;
            }

            result.Add(new EmployeeImportRow
            {
                RowNumber = line.RowNumber,
                Matricola = GetColumn(columns, 0),
                FullName = GetColumn(columns, 1),
                Email = NormalizeNullable(GetColumn(columns, 2)),
                OrgUnitName = NormalizeOptionalImportValue(GetColumn(columns, 3)),
                JobRoleName = NormalizeOptionalImportValue(GetColumn(columns, 4)),
                IsActive = ParseBool(GetColumn(columns, 5))
            });
        }

        return result;
    }

    private static string? ValidateImportRow(
        EmployeeImportRow row,
        bool updateExisting,
        Dictionary<string, Employee> existingByMatricola,
        Dictionary<string, OrgUnit> orgUnitByName,
        Dictionary<string, JobRole> jobRoleByName,
        HashSet<string> seenMatricole)
    {
        if (string.IsNullOrWhiteSpace(row.Matricola))
        {
            return "Matricola obbligatoria.";
        }

        if (string.IsNullOrWhiteSpace(row.FullName))
        {
            return "FullName obbligatorio.";
        }

        if (!seenMatricole.Add(row.Matricola))
        {
            return "Matricola duplicata nel file.";
        }

        if (!updateExisting && existingByMatricola.ContainsKey(row.Matricola))
        {
            return "Matricola gia esistente e update disabilitato.";
        }

        if (!string.IsNullOrWhiteSpace(row.OrgUnitName) && !orgUnitByName.ContainsKey(row.OrgUnitName))
        {
            return "OrgUnit non trovata.";
        }

        if (!string.IsNullOrWhiteSpace(row.JobRoleName) && !jobRoleByName.ContainsKey(row.JobRoleName))
        {
            return "JobRole non trovato.";
        }

        return null;
    }

    private static string[] SplitImportLine(string line)
    {
        var separator = line.Contains(';') ? ';' : ',';
        return line.Split(separator).Select(x => x.Trim()).ToArray();
    }

    private static bool IsHeader(string[] columns)
        => columns.Length > 0 && string.Equals(columns[0], "Matricola", StringComparison.OrdinalIgnoreCase);

    private static string GetColumn(string[] columns, int index)
        => columns.Length > index ? columns[index].Trim() : string.Empty;

    private static bool ParseBool(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Trim().Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Trim().Equals("si", StringComparison.OrdinalIgnoreCase)
            || value.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private static Guid? ResolveOrgUnitId(string? name, Dictionary<string, OrgUnit> orgUnitByName)
        => string.IsNullOrWhiteSpace(name) ? null : orgUnitByName[name].Id;

    private static Guid? ResolveJobRoleId(string? name, Dictionary<string, JobRole> jobRoleByName)
        => string.IsNullOrWhiteSpace(name) ? null : jobRoleByName[name].Id;

    private class EmployeeImportRow
    {
        public int RowNumber { get; set; }
        public string Matricola { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? OrgUnitName { get; set; }
        public string? JobRoleName { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
