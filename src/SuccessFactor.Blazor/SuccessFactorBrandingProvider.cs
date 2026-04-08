using Microsoft.Extensions.Localization;
using SuccessFactor.Localization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Ui.Branding;

namespace SuccessFactor.Blazor;

[Dependency(ReplaceServices = true)]
public class SuccessFactorBrandingProvider : DefaultBrandingProvider
{
    private IStringLocalizer<SuccessFactorResource> _localizer;

    public SuccessFactorBrandingProvider(IStringLocalizer<SuccessFactorResource> localizer)
    {
        _localizer = localizer;
    }

    public override string AppName => _localizer["AppName"];
}
