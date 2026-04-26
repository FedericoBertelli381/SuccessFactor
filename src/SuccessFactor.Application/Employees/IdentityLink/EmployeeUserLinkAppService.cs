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
    private readonly IRepository<IdentityRole, Guid> _roleRepo;
    private readonly IdentityUserManager _identityUserManager;
    private readonly IBusinessAuditLogger _auditLogger;

    public EmployeeUserLinkAppService(
        IRepository<Employee, Guid> employeeRepo,
        IRepository<IdentityUser, Guid> userRepo,
        IRepository<IdentityRole, Guid> roleRepo,
        IdentityUserManager identityUserManager,
        IBusinessAuditLogger auditLogger)
    {
        _employeeRepo = employeeRepo;
        _userRepo = userRepo;
        _roleRepo = roleRepo;
        _identityUserManager = identityUserManager;
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
        var userById = users.ToDictionary(x => x.Id);
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

    public async Task<UserRoleImportResultDto> ImportRolesAsync(ImportUserRolesInput input)
    {
        EnsureTenantAndAdmin();

        if (input is null || string.IsNullOrWhiteSpace(input.Content))
        {
            throw new BusinessException("UserRoleImportContentRequired");
        }

        var allowedRoles = new[]
        {
            SuccessFactorRoles.Admin,
            SuccessFactorRoles.Hr,
            SuccessFactorRoles.Manager,
            SuccessFactorRoles.Employee
        };

        var userQuery = await _userRepo.GetQueryableAsync();
        var users = await AsyncExecuter.ToListAsync(userQuery);
        var userById = users.ToDictionary(x => x.Id);
        var usersByUserName = users
            .Where(x => !string.IsNullOrWhiteSpace(x.UserName))
            .GroupBy(x => x.UserName!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);
        var usersByEmail = users
            .Where(x => !string.IsNullOrWhiteSpace(x.Email))
            .GroupBy(x => x.Email!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);

        var employeeQuery = await _employeeRepo.GetQueryableAsync();
        var employees = await AsyncExecuter.ToListAsync(employeeQuery);
        var employeeByMatricola = employees.ToDictionary(x => x.Matricola, StringComparer.OrdinalIgnoreCase);

        var roleQuery = await _roleRepo.GetQueryableAsync();
        var existingRoles = await AsyncExecuter.ToListAsync(roleQuery);
        var existingRoleNames = new HashSet<string>(existingRoles.Select(x => x.Name).Where(x => !string.IsNullOrWhiteSpace(x))!, StringComparer.OrdinalIgnoreCase);

        var parsedRows = ParseRoleImportRows(input.Content);
        var result = new UserRoleImportResultDto();
        var validRows = new List<ResolvedRoleImportRow>();
        var seenUserIds = new HashSet<Guid>();

        foreach (var row in parsedRows)
        {
            var validationMessage = ValidateRoleImportRow(
                row,
                usersByUserName,
                usersByEmail,
                employeeByMatricola,
                userById,
                allowedRoles,
                existingRoleNames,
                seenUserIds,
                out var resolvedUser,
                out var resolvedRoles,
                out var resolvedMode);

            if (!string.IsNullOrWhiteSpace(validationMessage))
            {
                result.Rows.Add(new UserRoleImportRowResultDto
                {
                    RowNumber = row.RowNumber,
                    UserName = row.UserName,
                    Email = row.Email,
                    Matricola = row.Matricola,
                    Roles = row.RolesRaw,
                    Mode = row.Mode,
                    Status = "Error",
                    Message = validationMessage
                });
                continue;
            }

            seenUserIds.Add(resolvedUser!.Id);
            validRows.Add(new ResolvedRoleImportRow
            {
                RowNumber = row.RowNumber,
                User = resolvedUser,
                Roles = resolvedRoles!,
                Mode = resolvedMode
            });

            result.Rows.Add(new UserRoleImportRowResultDto
            {
                RowNumber = row.RowNumber,
                UserName = resolvedUser.UserName,
                Email = resolvedUser.Email,
                Matricola = row.Matricola,
                Roles = string.Join(", ", resolvedRoles!),
                Mode = resolvedMode,
                Status = "Ready"
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
            var currentRoles = (await _identityUserManager.GetRolesAsync(row.User))
                .Where(x => SuccessFactorRoles.Normalize(x) is not null)
                .Select(x => SuccessFactorRoles.Normalize(x)!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var targetRoles = row.Roles
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var rolesToAdd = new List<string>();
            var rolesToRemove = new List<string>();

            if (row.Mode == "Replace")
            {
                rolesToRemove.AddRange(currentRoles.Except(targetRoles, StringComparer.OrdinalIgnoreCase));
            }

            rolesToAdd.AddRange(targetRoles.Except(currentRoles, StringComparer.OrdinalIgnoreCase));

            if (rolesToRemove.Count > 0)
            {
                var removeResult = await _identityUserManager.RemoveFromRolesAsync(row.User, rolesToRemove);
                EnsureIdentityResultSucceeded(removeResult);
                result.RemovedAssignmentsCount += rolesToRemove.Count;
            }

            if (rolesToAdd.Count > 0)
            {
                var addResult = await _identityUserManager.AddToRolesAsync(row.User, rolesToAdd);
                EnsureIdentityResultSucceeded(addResult);
                result.AddedAssignmentsCount += rolesToAdd.Count;
            }

            result.ProcessedCount++;
        }

        await CurrentUnitOfWork.SaveChangesAsync();
        await _auditLogger.LogAsync("UserRoleImportCompleted", "UserRoleImport", null, new Dictionary<string, object?>
        {
            ["ProcessedCount"] = result.ProcessedCount,
            ["AddedAssignmentsCount"] = result.AddedAssignmentsCount,
            ["RemovedAssignmentsCount"] = result.RemovedAssignmentsCount,
            ["RowsCount"] = result.Rows.Count
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

    private static void EnsureIdentityResultSucceeded(Microsoft.AspNetCore.Identity.IdentityResult result)
    {
        if (result.Succeeded)
        {
            return;
        }

        var message = string.Join("; ", result.Errors.Select(x => x.Description));
        throw new BusinessException("IdentityRoleUpdateFailed")
            .WithData("Reason", message);
    }

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

    private static List<RoleImportRow> ParseRoleImportRows(string content)
    {
        var result = new List<RoleImportRow>();
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
            if (columns.Length > 0 && string.Equals(columns[0], "UserName", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(new RoleImportRow
            {
                RowNumber = line.RowNumber,
                UserName = NormalizeOptionalImportValue(GetColumn(columns, 0)),
                Email = NormalizeOptionalImportValue(GetColumn(columns, 1)),
                Matricola = NormalizeOptionalImportValue(GetColumn(columns, 2)),
                RolesRaw = GetColumn(columns, 3),
                Mode = NormalizeRoleImportMode(GetColumn(columns, 4))
            });
        }

        return result;
    }

    private static string? ValidateRoleImportRow(
        RoleImportRow row,
        Dictionary<string, List<IdentityUser>> usersByUserName,
        Dictionary<string, List<IdentityUser>> usersByEmail,
        Dictionary<string, Employee> employeeByMatricola,
        Dictionary<Guid, IdentityUser> userById,
        string[] allowedRoles,
        HashSet<string> existingRoleNames,
        HashSet<Guid> seenUserIds,
        out IdentityUser? user,
        out List<string>? roles,
        out string mode)
    {
        user = null;
        roles = null;
        mode = row.Mode;

        var roleTokens = SplitRoles(row.RolesRaw)
            .Select(SuccessFactorRoles.Normalize)
            .ToList();

        if (roleTokens.Count == 0)
        {
            return "Almeno un ruolo applicativo e obbligatorio.";
        }

        if (roleTokens.Any(x => x is null))
        {
            return "Uno o piu ruoli non sono riconosciuti.";
        }

        roles = roleTokens!
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var unsupported = roles.Except(allowedRoles, StringComparer.OrdinalIgnoreCase).ToList();
        if (unsupported.Count > 0)
        {
            return $"Ruoli non supportati: {string.Join(", ", unsupported)}.";
        }

        var missingInDb = roles.Where(x => !existingRoleNames.Contains(x)).ToList();
        if (missingInDb.Count > 0)
        {
            return $"Ruoli non presenti nel tenant: {string.Join(", ", missingInDb)}.";
        }

        var candidates = new List<IdentityUser>();

        if (!string.IsNullOrWhiteSpace(row.UserName))
        {
            candidates.AddRange(LookupUsers(usersByUserName, row.UserName));
        }

        if (!string.IsNullOrWhiteSpace(row.Email))
        {
            candidates.AddRange(LookupUsers(usersByEmail, row.Email));
        }

        if (!string.IsNullOrWhiteSpace(row.Matricola))
        {
            if (!employeeByMatricola.TryGetValue(row.Matricola, out var employee))
            {
                return "Matricola non trovata.";
            }

            if (!employee.UserId.HasValue)
            {
                return "La matricola indicata non ha un utente collegato.";
            }

            if (!userById.TryGetValue(employee.UserId.Value, out var linkedUser))
            {
                return "Utente collegato alla matricola non trovato.";
            }

            candidates.Add(linkedUser);
        }

        var distinctCandidates = candidates
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .ToList();

        if (distinctCandidates.Count == 0)
        {
            return "Nessun utente trovato con i riferimenti indicati.";
        }

        if (distinctCandidates.Count > 1)
        {
            return "Identificazione utente ambigua.";
        }

        user = distinctCandidates[0];

        if (seenUserIds.Contains(user.Id))
        {
            return "Lo stesso utente compare piu volte nel file.";
        }

        return null;
    }

    private static string NormalizeRoleImportMode(string? value)
    {
        var normalized = NormalizeOptionalImportValue(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "Add";
        }

        return normalized.Equals("Replace", StringComparison.OrdinalIgnoreCase)
            ? "Replace"
            : "Add";
    }

    private static List<string> SplitRoles(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(['|', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    private sealed class RoleImportRow
    {
        public int RowNumber { get; set; }
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string? Matricola { get; set; }
        public string RolesRaw { get; set; } = string.Empty;
        public string Mode { get; set; } = "Add";
    }

    private sealed class ResolvedRoleImportRow
    {
        public int RowNumber { get; set; }
        public IdentityUser User { get; set; } = default!;
        public List<string> Roles { get; set; } = [];
        public string Mode { get; set; } = "Add";
    }
}
