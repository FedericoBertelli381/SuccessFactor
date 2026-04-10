using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using SuccessFactor.Employees;

namespace SuccessFactor.Employees.IdentityLink;

public class EmployeeUserLinkAppService : ApplicationService, IEmployeeUserLinkAppService
{
    private readonly IRepository<Employee, Guid> _employeeRepo;
    private readonly IRepository<IdentityUser, Guid> _userRepo;

    public EmployeeUserLinkAppService(IRepository<Employee, Guid> employeeRepo, IRepository<IdentityUser, Guid> userRepo)
    {
        _employeeRepo = employeeRepo;
        _userRepo = userRepo;
    }

    public async Task<IdentityUserLookupDto[]> SearchUsersAsync(string? filter = null, int maxResultCount = 20)
    {
        EnsureTenant();

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
        EnsureTenant();

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
        EnsureTenant();

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

    public async Task LinkAsync(LinkEmployeeUserDto input)
    {
        EnsureTenant();

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
    }

    public async Task UnlinkAsync(Guid employeeId)
    {
        EnsureTenant();

        var emp = await _employeeRepo.GetAsync(employeeId);
        emp.UserId = null;
        await _employeeRepo.UpdateAsync(emp, autoSave: true);
    }

    // opzionale: link automatico per email (molto comodo)
    public async Task<bool> LinkByEmailAsync(Guid employeeId)
    {
        EnsureTenant();

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

    private void EnsureTenant()
    {
        if (CurrentTenant.Id == null)
            throw new BusinessException("TenantMissing")
                .WithData("Hint", "Aggiungi ?__tenant=NOME_TENANT alla chiamata e lavora nel tenant corretto.");
    }
}
