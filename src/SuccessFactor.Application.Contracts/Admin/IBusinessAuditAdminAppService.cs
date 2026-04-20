using System.Threading.Tasks;

namespace SuccessFactor.Admin;

public interface IBusinessAuditAdminAppService
{
    Task<BusinessAuditEventListDto> GetAsync(GetBusinessAuditEventsInput input);
}
