using System.Threading.Tasks;

namespace SuccessFactor.Data;

public interface ISuccessFactorDbSchemaMigrator
{
    Task MigrateAsync();
}
