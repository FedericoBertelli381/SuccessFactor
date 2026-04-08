using SuccessFactor.Employees;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace SuccessFactor.Team.Support;

public class ManagerScopeResolver : IManagerScopeResolver, ITransientDependency
{
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IRepository<EmployeeManager, Guid> _employeeManagerRepository;
    private readonly IRepository<Employee, Guid> _employeeRepository;

    public ManagerScopeResolver(
        IAsyncQueryableExecuter asyncExecuter,
        IRepository<EmployeeManager, Guid> employeeManagerRepository,
        IRepository<Employee, Guid> employeeRepository)
    {
        _asyncExecuter = asyncExecuter;
        _employeeManagerRepository = employeeManagerRepository;
        _employeeRepository = employeeRepository;
    }

    public async Task<List<Guid>> GetManagedEmployeeIdsAsync(Guid managerEmployeeId)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var relationQuery = await _employeeManagerRepository.GetQueryableAsync();

        var rawManagedEmployeeIds = await _asyncExecuter.ToListAsync(
            relationQuery
                .Where(x =>
                    x.ManagerEmployeeId == managerEmployeeId &&
                    (!x.StartDate.HasValue || x.StartDate.Value <= today) &&
                    (!x.EndDate.HasValue || x.EndDate.Value >= today))
                .Select(x => x.EmployeeId)
                .Distinct());

        if (rawManagedEmployeeIds.Count == 0)
        {
            return new List<Guid>();
        }

        var employeeQuery = await _employeeRepository.GetQueryableAsync();

        var activeManagedEmployeeIds = await _asyncExecuter.ToListAsync(
            employeeQuery
                .Where(x => rawManagedEmployeeIds.Contains(x.Id) && x.IsActive)
                .Select(x => x.Id)
                .Distinct());

        return activeManagedEmployeeIds;
    }
}