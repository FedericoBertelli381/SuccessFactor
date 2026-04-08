using SuccessFactor.Localization;
using Volo.Abp.AspNetCore.Components;

namespace SuccessFactor.Blazor;

public abstract class SuccessFactorComponentBase : AbpComponentBase
{
    protected SuccessFactorComponentBase()
    {
        LocalizationResource = typeof(SuccessFactorResource);
    }
}
