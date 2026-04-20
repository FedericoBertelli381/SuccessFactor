using System.Threading.Tasks;

namespace SuccessFactor.Admin;

public interface INotificationAdminAppService
{
    Task<NotificationReadinessDto> GetReadinessAsync();
    Task SendTestEmailAsync(SendTestNotificationInput input);
}
