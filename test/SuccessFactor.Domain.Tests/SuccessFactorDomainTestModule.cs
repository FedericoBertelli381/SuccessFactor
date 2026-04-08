using Volo.Abp.Modularity;

namespace SuccessFactor;

[DependsOn(
    typeof(SuccessFactorDomainModule),
    typeof(SuccessFactorTestBaseModule)
)]
public class SuccessFactorDomainTestModule : AbpModule
{

}
