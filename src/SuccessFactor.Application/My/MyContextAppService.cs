using SuccessFactor.Employees;
using SuccessFactor.My;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Users;

namespace SuccessFactor.My;

public class MyContextAppService : ApplicationService
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
            throw new BusinessException("EmployeeNotLinkedToUser")
                .WithData("Hint", "Collega Employees.UserId all'utente ABP (AbpUsers.Id).");

        var date = asOfDate ?? DateOnly.FromDateTime(Clock.Now);

        // RoleCodes: Employee sempre, Manager se ha subordinati attivi, HR se ha ruolo ABP “hr”
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

    private async Task<string[]> ResolveRoleCodesAsync(Guid actorEmployeeId, string[] abpRoles, DateOnly date)
    {
        bool isHr = abpRoles.Any(r => r.Contains("hr", StringComparison.OrdinalIgnoreCase));

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
}