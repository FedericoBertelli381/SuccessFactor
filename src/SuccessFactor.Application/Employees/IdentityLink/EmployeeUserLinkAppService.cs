using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using SuccessFactor.Auditing;
using SuccessFactor.Employees;
using SuccessFactor.Security;

namespace SuccessFactor.Employees.IdentityLink;

[Authorize]
public class EmployeeUserLinkAppService : ApplicationService, IEmployeeUserLinkAppService
{
    private readonly IRepository<Employee, Guid> _employeeRepo;
    private readonly IRepository<IdentityUser, Guid> _userRepo;
    private readonly IBusinessAuditLogger _auditLogger;

    public EmployeeUserLinkAppService(
        IRepository<Employee, Guid> employeeRepo,
        IRepository<IdentityUser, Guid> userRepo,
        IBusinessAuditLogger auditLogger)
    {
        _employeeRepo = employeeRepo;
        _userRepo = userRepo;
        _auditLogger = auditLogger;
    }

    public async Task<IdentityUserLookupDto[]> SearchUsersAsync(string? filter = null, int maxResultCount = 20)
    {
        EnsureTenantAndAdmin();
        maxResultCount = Math.Clamp(maxResultCount, 1, 100);

        var q = await _userRepo.GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(filter))
        {
            q = q.Where(u =>
                (u.UserName != null && u.UserName.Contains(filter)) ||
                (u.Email != null && u.Email.Contains(filter)) ||
                (u.Name != null && u.Name.Contains(filter)) ||
                (u.Surname != null && u.Surname.Contains(filter)));
        }

        var list = await AsyncExecuter.ToListAsync(
            q.OrderBy(u => u.UserName).Take(maxResultCount)
        );

        return list.Select(u => new IdentityUserLookupDto
        {
            Id = u.Id,
            UserName = u.UserName ?? "",
            Email = u.Email,
            Name = u.Name,
            Surname = u.Surname
        }).ToArray();
    }

    public async Task<UnlinkedEmployeeDto[]> GetUnlinkedEmployeesAsync(int maxResultCount = 50)
    {
        EnsureTenantAndAdmin();
        maxResultCount = Math.Clamp(maxResultCount, 1, 200);

        var q = await _employeeRepo.GetQueryableAsync();

        var list = await AsyncExecuter.ToListAsync(
            q.Where(e => e.UserId == null)
             .OrderBy(e => e.Matricola)
             .Take(maxResultCount)
        );

        return list.Select(e => new UnlinkedEmployeeDto
        {
            EmployeeId = e.Id,
            Matricola = e.Matricola,
            FullName = e.FullName,
            Email = e.Email
        }).ToArray();
    }

    public async Task<LinkedEmployeeDto[]> GetLinkedEmployeesAsync(int maxResultCount = 100)
    {
        EnsureTenantAndAdmin();
        maxResultCount = Math.Clamp(maxResultCount, 1, 500);

        var employeeQuery = await _employeeRepo.GetQueryableAsync();
        var userQuery = await _userRepo.GetQueryableAsync();

        var list = await AsyncExecuter.ToListAsync(
            (from employee in employeeQuery
             join user in userQuery on employee.UserId equals user.Id
             where employee.UserId != null
             orderby employee.Matricola
             select new
             {
                 employee.Id,
                 employee.UserId,
                 employee.Matricola,
                 employee.FullName,
                 EmployeeEmail = employee.Email,
                 UserName = user.UserName,
                 UserEmail = user.Email
             })
            .Take(maxResultCount));

        return list.Select(x => new LinkedEmployeeDto
        {
            EmployeeId = x.Id,
            UserId = x.UserId!.Value,
            Matricola = x.Matricola,
            FullName = x.FullName,
            EmployeeEmail = x.EmployeeEmail,
            UserName = x.UserName ?? string.Empty,
            UserEmail = x.UserEmail
        }).ToArray();
    }

    public async Task<EmployeeUserLinkImportResultDto> ImportAsync(ImportEmployeeUserLinksInput input)
    {
        EnsureTenantAndAdmin();

        if (input is null || string.IsNullOrWhiteSpace(input.Content))
        {
            throw new BusinessException("IdentityLinkImportContentRequired");
        }

        var employeeQuery = await _employeeRepo.GetQueryableAsync();
        var employees = await AsyncExecuter.ToListAsync(employeeQuery);
        var employeeByMatricola = employees.ToDictionary(x => x.Matricola, StringComparer.OrdinalIgnoreCase);

        var userQuery = await _userRepo.GetQueryableAsync();
        var users = await AsyncExecuter.ToListAsync(userQuery);
        var usersByUserName = users
            .Where(x => !string.IsNullOrWhiteSpace(x.UserName))
            .GroupBy(x => x.UserName!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);
        var usersByEmail = users
            .Where(x => !string.IsNullOrWhiteSpace(x.Email))
            .GroupBy(x => x.Email!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);
        var employeeByUserId = employees
            .Where(x => x.UserId.HasValue)
            .GroupBy(x => x.UserId!.Value)
            .ToDictionary(x => x.Key, x => x.First());

        var parsedRows = ParseImportRows(input.Content);
        var result = new EmployeeUserLinkImportResultDto();
        var validRows = new List<ResolvedImportRow>();
        var seenMatricole = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenUserIds = new Dictionary<Guid, string>();

        foreach (var row in parsedRows)
        {
            var validationMessage = ValidateImportRow(
                row,
                input.UpdateExistingLinks,
                employeeByMatricola,
                usersByUserName,
                usersByEmail,
                seenMatricole,
                seenUserIds,
                employeeByUserId,
                out var resolvedEmployee,
                out var resolvedUser,
                out var isRelink);

            if (!string.IsNullOrWhiteSpace(validationMessage))
            {
                result.Rows.Add(new EmployeeUserLinkImportRowResultDto
                {
                    RowNumber = row.RowNumber,
                    Matricola = row.Matricola,
                    UserName = row.UserName,
                    Email = row.Email,
                    MatchMode = row.MatchMode,
                    Status = "Error",
                    Message = validationMessage
                });
                continue;
            }

            seenUserIds[resolvedUser!.Id] = row.Matricola;
            validRows.Add(new ResolvedImportRow
            {
                RowNumber = row.RowNumber,
                Matricola = row.Matricola,
                MatchMode = row.MatchMode,
                UserName = row.UserName,
                Email = row.Email,
                Employee = resolvedEmployee!,
                User = resolvedUser,
                IsRelink = isRelink
            });

            result.Rows.Add(new EmployeeUserLinkImportRowResultDto
            {
                RowNumber = row.RowNumber,
                Matricola = row.Matricola,
                UserName = resolvedUser.UserName,
                Email = resolvedUser.Email,
                MatchMode = row.MatchMode,
                Status = isRelink ? "Relink" : "Link"
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
            row.Employee.UserId = row.User.Id;

            if (string.IsNullOrWhiteSpace(row.Employee.Email) && !string.IsNullOrWhiteSpace(row.User.Email))
            {
                row.Employee.Email = row.User.Email;
            }

            await _employeeRepo.UpdateAsync(row.Employee, autoSave: false);

            if (row.IsRelink)
            {
                result.RelinkedCount++;
            }
            else
            {
                result.LinkedCount++;
            }
        }

        await CurrentUnitOfWork.SaveChangesAsync();
        await _auditLogger.LogAsync("EmployeeUserLinkImportCompleted", "EmployeeUserLinkImport", null, new Dictionary<string, object?>
        {
            ["LinkedCount"] = result.LinkedCount,
            ["RelinkedCount"] = result.RelinkedCount,
            ["RowsCount"] = result.Rows.Count,
            ["UpdateExistingLinks"] = input.UpdateExistingLinks
        });

        return result;
    }

    public async Task LinkAsync(LinkEmployeeUserDto input)
    {
        EnsureTenantAndAdmin();

        var emp = await _employeeRepo.GetAsync(input.EmployeeId);

        var user = await _userRepo.GetAsync(input.UserId);

        // sicurezza: user nello stesso tenant
        if (user.TenantId != CurrentTenant.Id)
            throw new BusinessException("UserTenantMismatch");

        // evita che lo stesso user venga collegato a 2 employee
        var alreadyLinked = await _employeeRepo.AnyAsync(e => e.UserId == input.UserId && e.Id != input.EmployeeId);
        if (alreadyLinked)
            throw new BusinessException("UserAlreadyLinkedToAnotherEmployee");

        emp.UserId = input.UserId;

        // opzionale: se email employee vuota, copiala dall'utente
        if (string.IsNullOrWhiteSpace(emp.Email) && !string.IsNullOrWhiteSpace(user.Email))
            emp.Email = user.Email;

        await _employeeRepo.UpdateAsync(emp, autoSave: true);
        await _auditLogger.LogAsync("EmployeeUserLinked", nameof(Employee), emp.Id.ToString(), new Dictionary<string, object?>
        {
            ["EmployeeId"] = emp.Id,
            ["LinkedUserId"] = input.UserId
        });
    }

    public async Task UnlinkAsync(Guid employeeId)
    {
        EnsureTenantAndAdmin();

        var emp = await _employeeRepo.GetAsync(employeeId);
        var previousUserId = emp.UserId;
        emp.UserId = null;
        await _employeeRepo.UpdateAsync(emp, autoSave: true);
        await _auditLogger.LogAsync("EmployeeUserUnlinked", nameof(Employee), emp.Id.ToString(), new Dictionary<string, object?>
        {
            ["EmployeeId"] = emp.Id,
            ["PreviousUserId"] = previousUserId
        });
    }

    // opzionale: link automatico per email (molto comodo)
    public async Task<bool> LinkByEmailAsync(Guid employeeId)
    {
        EnsureTenantAndAdmin();

        var emp = await _employeeRepo.GetAsync(employeeId);
        if (string.IsNullOrWhiteSpace(emp.Email))
            throw new BusinessException("EmployeeEmailMissing");

        var q = await _userRepo.GetQueryableAsync();
        var user = await AsyncExecuter.FirstOrDefaultAsync(
            q.Where(u => u.Email == emp.Email)
        );

        if (user == null) return false;

        await LinkAsync(new LinkEmployeeUserDto { EmployeeId = employeeId, UserId = user.Id });
        return true;
    }

    private void EnsureTenantAndAdmin()
    {
        if (CurrentTenant.Id == null)
            throw new BusinessException("TenantMissing")
                .WithData("Hint", "Aggiungi ?__tenant=NOME_TENANT alla chiamata e lavora nel tenant corretto.");

        var roles = CurrentUser.Roles ?? Array.Empty<string>();

        if (!SuccessFactorRoles.IsAdmin(roles))
        {
            throw new BusinessException("CurrentUserIsNotAdmin");
        }
    }

    private static List<IdentityLinkImportRow> ParseImportRows(string content)
    {
        var result = new List<IdentityLinkImportRow>();
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

            result.Add(new IdentityLinkImportRow
            {
                RowNumber = line.RowNumber,
                Matricola = GetColumn(columns, 0),
                UserName = NormalizeOptionalImportValue(GetColumn(columns, 1)),
                Email = NormalizeOptionalImportValue(GetColumn(columns, 2)),
                MatchMode = NormalizeMatchMode(GetColumn(columns, 3))
            });
        }

        return result;
    }

    private static string? ValidateImportRow(
        IdentityLinkImportRow row,
        bool updateExistingLinks,
        Dictionary<string, Employee> employeeByMatricola,
        Dictionary<string, List<IdentityUser>> usersByUserName,
        Dictionary<string, List<IdentityUser>> usersByEmail,
        HashSet<string> seenMatricole,
        Dictionary<Guid, string> seenUserIds,
        Dictionary<Guid, Employee> employeeByUserId,
        out Employee? employee,
        out IdentityUser? user,
        out bool isRelink)
    {
        employee = null;
        user = null;
        isRelink = false;

        if (string.IsNullOrWhiteSpace(row.Matricola))
        {
            return "Matricola obbligatoria.";
        }

        if (!seenMatricole.Add(row.Matricola))
        {
            return "Matricola duplicata nel file.";
        }

        if (!employeeByMatricola.TryGetValue(row.Matricola, out employee))
        {
            return "Employee non trovato.";
        }

        if (employee.UserId.HasValue && !updateExistingLinks)
        {
            return "Employee gia collegato e update disabilitato.";
        }

        var candidates = ResolveCandidateUsers(row, employee, usersByUserName, usersByEmail, out var resolveMessage);
        if (!string.IsNullOrWhiteSpace(resolveMessage))
        {
            return resolveMessage;
        }

        var distinctCandidates = candidates
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .ToList();

        if (distinctCandidates.Count == 0)
        {
            return "Nessun utente trovato.";
        }

        if (distinctCandidates.Count > 1)
        {
            return "Match ambiguo: piu utenti candidati trovati.";
        }

        user = distinctCandidates[0];

        if (seenUserIds.TryGetValue(user.Id, out var alreadyAssignedTo) &&
            !string.Equals(alreadyAssignedTo, row.Matricola, StringComparison.OrdinalIgnoreCase))
        {
            return $"Lo stesso utente e gia assegnato nel file alla matricola {alreadyAssignedTo}.";
        }

        if (employeeByUserId.TryGetValue(user.Id, out var linkedEmployee) && linkedEmployee.Id != employee.Id)
        {
            return $"Utente gia collegato alla matricola {linkedEmployee.Matricola}.";
        }

        if (employee.UserId.HasValue && employee.UserId.Value != user.Id)
        {
            isRelink = true;
        }

        return null;
    }

    private static List<IdentityUser> ResolveCandidateUsers(
        IdentityLinkImportRow row,
        Employee employee,
        Dictionary<string, List<IdentityUser>> usersByUserName,
        Dictionary<string, List<IdentityUser>> usersByEmail,
        out string? message)
    {
        message = null;
        var candidates = new List<IdentityUser>();

        switch (row.MatchMode)
        {
            case "UserName":
                if (string.IsNullOrWhiteSpace(row.UserName))
                {
                    message = "UserName obbligatorio con MatchMode UserName.";
                    return candidates;
                }

                candidates.AddRange(LookupUsers(usersByUserName, row.UserName));
                return candidates;

            case "Email":
                var email = row.Email ?? employee.Email;
                if (string.IsNullOrWhiteSpace(email))
                {
                    message = "Email obbligatoria con MatchMode Email.";
                    return candidates;
                }

                candidates.AddRange(LookupUsers(usersByEmail, email));
                return candidates;

            default:
                if (!string.IsNullOrWhiteSpace(row.UserName))
                {
                    candidates.AddRange(LookupUsers(usersByUserName, row.UserName));
                }

                if (!string.IsNullOrWhiteSpace(row.Email))
                {
                    candidates.AddRange(LookupUsers(usersByEmail, row.Email));
                }
                else if (!string.IsNullOrWhiteSpace(employee.Email))
                {
                    candidates.AddRange(LookupUsers(usersByEmail, employee.Email));
                }

                if (candidates.Count == 0)
                {
                    message = "Nessun criterio di match risolvibile. Valorizza UserName o Email, oppure allinea l'email employee.";
                }

                return candidates;
        }
    }

    private static List<IdentityUser> LookupUsers(Dictionary<string, List<IdentityUser>> index, string key)
        => index.TryGetValue(key, out var users) ? users : [];

    private static string NormalizeMatchMode(string? value)
    {
        var normalized = NormalizeOptionalImportValue(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "Auto";
        }

        if (normalized.Equals("Email", StringComparison.OrdinalIgnoreCase))
        {
            return "Email";
        }

        if (normalized.Equals("UserName", StringComparison.OrdinalIgnoreCase))
        {
            return "UserName";
        }

        return "Auto";
    }

    private static string? NormalizeOptionalImportValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized == "-" ? null : normalized;
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

    private sealed class IdentityLinkImportRow
    {
        public int RowNumber { get; set; }
        public string Matricola { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string MatchMode { get; set; } = "Auto";
    }

    private sealed class ResolvedImportRow
    {
        public int RowNumber { get; set; }
        public string Matricola { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string MatchMode { get; set; } = "Auto";
        public Employee Employee { get; set; } = default!;
        public IdentityUser User { get; set; } = default!;
        public bool IsRelink { get; set; }
    }
}
