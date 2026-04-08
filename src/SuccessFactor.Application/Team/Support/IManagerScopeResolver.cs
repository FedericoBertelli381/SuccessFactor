using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SuccessFactor.Team.Support;

public interface IManagerScopeResolver
{
    Task<List<Guid>> GetManagedEmployeeIdsAsync(Guid managerEmployeeId);
}