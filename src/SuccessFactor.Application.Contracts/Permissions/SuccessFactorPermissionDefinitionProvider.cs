using SuccessFactor.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;
using Volo.Abp.MultiTenancy;

namespace SuccessFactor.Permissions;

public class SuccessFactorPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(SuccessFactorPermissions.GroupName);

        //Define your own permissions here. Example:
        //myGroup.AddPermission(SuccessFactorPermissions.MyPermission1, L("Permission:MyPermission1"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<SuccessFactorResource>(name);
    }
}
