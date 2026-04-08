using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SuccessFactor.EntityFrameworkCore;

/* This class is needed for EF Core console commands
 * (like Add-Migration and Update-Database commands) */
public class SuccessFactorDbContextFactory : IDesignTimeDbContextFactory<SuccessFactorDbContext>
{
    public SuccessFactorDbContext CreateDbContext(string[] args)
    {
        var configuration = BuildConfiguration();
        
        SuccessFactorEfCoreEntityExtensionMappings.Configure();

        var builder = new DbContextOptionsBuilder<SuccessFactorDbContext>()
            .UseSqlServer(configuration.GetConnectionString("Default"));
        
        return new SuccessFactorDbContext(builder.Options);
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../SuccessFactor.DbMigrator/"))
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables();

        return builder.Build();
    }
}
