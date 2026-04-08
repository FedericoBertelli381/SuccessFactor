using Volo.Abp.Modularity;

namespace SuccessFactor;

[DependsOn(
    typeof(SuccessFactorApplicationModule),
    typeof(SuccessFactorDomainTestModule)
)]
public class SuccessFactorApplicationTestModule : AbpModule
{

}
