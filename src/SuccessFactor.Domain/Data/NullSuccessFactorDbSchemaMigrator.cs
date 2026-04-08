using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace SuccessFactor.Data;

/* This is used if database provider does't define
 * ISuccessFactorDbSchemaMigrator implementation.
 */
public class NullSuccessFactorDbSchemaMigrator : ISuccessFactorDbSchemaMigrator, ITransientDependency
{
    public Task MigrateAsync()
    {
        return Task.CompletedTask;
    }
}
