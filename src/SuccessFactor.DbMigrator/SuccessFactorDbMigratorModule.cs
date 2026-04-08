using SuccessFactor.EntityFrameworkCore;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace SuccessFactor.DbMigrator;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(SuccessFactorEntityFrameworkCoreModule),
    typeof(SuccessFactorApplicationContractsModule)
)]
public class SuccessFactorDbMigratorModule : AbpModule
{
}
