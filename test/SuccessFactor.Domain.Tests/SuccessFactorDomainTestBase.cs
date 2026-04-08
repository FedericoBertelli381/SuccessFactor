using Volo.Abp.Modularity;

namespace SuccessFactor;

/* Inherit from this class for your domain layer tests. */
public abstract class SuccessFactorDomainTestBase<TStartupModule> : SuccessFactorTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
