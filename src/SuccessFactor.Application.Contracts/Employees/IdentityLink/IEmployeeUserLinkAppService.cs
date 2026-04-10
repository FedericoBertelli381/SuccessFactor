using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace SuccessFactor.Employees.IdentityLink;

public interface IEmployeeUserLinkAppService : IApplicationService
{
    Task<IdentityUserLookupDto[]> SearchUsersAsync(string? filter = null, int maxResultCount = 20);
    Task<UnlinkedEmployeeDto[]> GetUnlinkedEmployeesAsync(int maxResultCount = 50);
    Task<LinkedEmployeeDto[]> GetLinkedEmployeesAsync(int maxResultCount = 100);
    Task LinkAsync(LinkEmployeeUserDto input);
    Task UnlinkAsync(Guid employeeId);
    Task<bool> LinkByEmailAsync(Guid employeeId);
}
