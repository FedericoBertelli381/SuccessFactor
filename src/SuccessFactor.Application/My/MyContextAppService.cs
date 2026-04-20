using SuccessFactor.Employees;
using SuccessFactor.My;
using SuccessFactor.Security;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Users;

namespace SuccessFactor.My;

[Authorize]
public class MyContextAppService : ApplicationService, IMyContextAppService
{
    private readonly IRepository<Employee, Guid> _employeeRepo;
    private readonly IRepository<EmployeeManager, Guid> _employeeManagerRepo;

    public MyContextAppService(
        IRepository<Employee, Guid> employeeRepo,
        IRepository<EmployeeManager, Guid> employeeManagerRepo)
    {
        _employeeRepo = employeeRepo;
        _employeeManagerRepo = employeeManagerRepo;
    }

    public async Task<MyContextDto> GetAsync(DateOnly? asOfDate = null)
    {
        EnsureTenantAndUser();

        var userId = CurrentUser.Id!.Value;
        var emp = await _employeeRepo.FirstOrDefaultAsync(e => e.UserId == userId);

        if (emp == null)
            throw new BusinessException(
                    code: "EmployeeNotLinkedToUser",
                    message: "Utente non collegato a Employee. Collega l'utente ABP a un record Employee.")
                .WithData("Hint", "Collega Employees.UserId all'utente ABP (AbpUsers.Id).");

        var date = asOfDate ?? DateOnly.FromDateTime(Clock.Now);

        // RoleCodes: Employee sempre, Manager se ha subordinati attivi, HR se ha ruolo ABP HR/admin.
        var abpRoles = (CurrentUser.Roles ?? Array.Empty<string>()).ToArray();
        var roleCodes = await ResolveRoleCodesAsync(emp.Id, abpRoles, date);

        return new MyContextDto
        {
            TenantId = CurrentTenant.Id!.Value,
            UserId = userId,
            EmployeeId = emp.Id,
            FullName = emp.FullName,
            Matricola = emp.Matricola,
            Email = emp.Email,
            OrgUnitId = emp.OrgUnitId,
            JobRoleId = emp.JobRoleId,
            AbpRoles = abpRoles,
            RoleCodes = roleCodes
        };
    }

    public async Task<MyContextStatusDto> GetStatusAsync()
    {
        if (CurrentTenant.Id is null)
        {
            return CreateErrorStatus("TenantMissing");
        }

        if (CurrentUser.Id is null)
        {
            return CreateErrorStatus("UserNotAuthenticated", CurrentTenant.Id);
        }

        var userId = CurrentUser.Id.Value;
        var emp = await _employeeRepo.FirstOrDefaultAsync(e => e.UserId == userId);

        if (emp is null)
        {
            return CreateErrorStatus("EmployeeNotLinkedToUser", CurrentTenant.Id, userId);
        }

        return new MyContextStatusDto
        {
            IsReady = true,
            TenantId = CurrentTenant.Id,
            UserId = userId,
            EmployeeId = emp.Id
        };
    }

    private async Task<string[]> ResolveRoleCodesAsync(Guid actorEmployeeId, string[] abpRoles, DateOnly date)
    {
        bool isHr = SuccessFactorRoles.IsAdminOrHr(abpRoles);

        bool isManager = await _employeeManagerRepo.AnyAsync(x =>
            x.ManagerEmployeeId == actorEmployeeId &&
            (!x.StartDate.HasValue || x.StartDate.Value <= date) &&
            (!x.EndDate.HasValue || x.EndDate.Value >= date));

        // sempre Employee se ha un record Employee
        return isHr
            ? (isManager ? new[] { "HR", "Manager", "Employee" } : new[] { "HR", "Employee" })
            : (isManager ? new[] { "Manager", "Employee" } : new[] { "Employee" });
    }

    private void EnsureTenantAndUser()
    {
        if (CurrentTenant.Id == null) throw new BusinessException("TenantMissing");
        if (CurrentUser.Id == null) throw new BusinessException("UserNotAuthenticated");
    }

    private static MyContextStatusDto CreateErrorStatus(
        string errorCode,
        Guid? tenantId = null,
        Guid? userId = null)
    {
        return new MyContextStatusDto
        {
            IsReady = false,
            TenantId = tenantId,
            UserId = userId,
            ErrorCode = errorCode,
            ErrorMessage = errorCode switch
            {
                "EmployeeNotLinkedToUser" =>
                    "Utente non collegato a Employee. Collega l'utente ABP a un record Employee prima di accedere all'area My.",
                "UserNotAuthenticated" => "Utente non autenticato.",
                "TenantMissing" => "Tenant non valorizzato.",
                _ => errorCode
            }
        };
    }
}
