using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace SuccessFactor.Hr;

public interface IHrReportsAppService : IApplicationService
{
    Task<HrReportDto> GetAsync(GetHrReportInput input);
    Task<HrExportFileDto> ExportCsvAsync(GetHrExportInput input);
}
