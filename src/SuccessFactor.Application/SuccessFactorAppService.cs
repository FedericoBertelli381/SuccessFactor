using SuccessFactor.Localization;
using Volo.Abp.Application.Services;

namespace SuccessFactor;

/* Inherit your application services from this class.
 */
public abstract class SuccessFactorAppService : ApplicationService
{
    protected SuccessFactorAppService()
    {
        LocalizationResource = typeof(SuccessFactorResource);
    }
}
