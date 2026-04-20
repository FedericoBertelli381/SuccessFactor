using System.Collections.Generic;
using System.Threading.Tasks;

namespace SuccessFactor.Auditing;

public interface IBusinessAuditLogger
{
    Task LogAsync(
        string action,
        string entityType,
        string? entityId = null,
        IReadOnlyDictionary<string, object?>? data = null);
}
