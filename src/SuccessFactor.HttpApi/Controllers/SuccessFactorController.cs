using SuccessFactor.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace SuccessFactor.Controllers;

/* Inherit your controllers from this class.
 */
public abstract class SuccessFactorController : AbpControllerBase
{
    protected SuccessFactorController()
    {
        LocalizationResource = typeof(SuccessFactorResource);
    }
}
