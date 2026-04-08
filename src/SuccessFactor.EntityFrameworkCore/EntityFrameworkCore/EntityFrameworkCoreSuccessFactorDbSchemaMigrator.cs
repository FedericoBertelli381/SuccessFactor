using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SuccessFactor.Data;
using Volo.Abp.DependencyInjection;

namespace SuccessFactor.EntityFrameworkCore;

public class EntityFrameworkCoreSuccessFactorDbSchemaMigrator
    : ISuccessFactorDbSchemaMigrator, ITransientDependency
{
    private readonly IServiceProvider _serviceProvider;

    public EntityFrameworkCoreSuccessFactorDbSchemaMigrator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task MigrateAsync()
    {
        /* We intentionally resolving the SuccessFactorDbContext
         * from IServiceProvider (instead of directly injecting it)
         * to properly get the connection string of the current tenant in the
         * current scope.
         */

        await _serviceProvider
            .GetRequiredService<SuccessFactorDbContext>()
            .Database
            .MigrateAsync();
    }
}
