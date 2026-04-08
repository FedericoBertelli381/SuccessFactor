using Volo.Abp.Settings;

namespace SuccessFactor.Settings;

public class SuccessFactorSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        //Define your own settings here. Example:
        //context.Add(new SettingDefinition(SuccessFactorSettings.MySetting1));
    }
}
