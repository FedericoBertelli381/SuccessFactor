using System;
using System.Threading.Tasks;
using SuccessFactor.Employees;
using Volo.Abp.Application.Services;

namespace SuccessFactor.Admin;

public interface IEmployeeAdminAppService : IApplicationService
{
    Task<EmployeeAdminDto> GetAsync();
    Task<EmployeeAdminListItemDto> SaveAsync(Guid? id, CreateUpdateEmployeeDto input);
}
