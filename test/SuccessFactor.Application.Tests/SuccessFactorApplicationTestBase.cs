using Volo.Abp.Modularity;

namespace SuccessFactor;

public abstract class SuccessFactorApplicationTestBase<TStartupModule> : SuccessFactorTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
