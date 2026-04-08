using SuccessFactor.Goals;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace SuccessFactor.Goals;

public class GoalTestAppService : ApplicationService
{
    private readonly IRepository<Goal, Guid> _goalRepo;

    public GoalTestAppService(IRepository<Goal, Guid> goalRepo)
    {
        _goalRepo = goalRepo;
    }

    // Chiamala da UI o da Swagger per verificare che EF mapping funzioni
    public async Task<long> CountAsync()
    {
        return await _goalRepo.GetCountAsync();
    }

    // utile per verificare velocemente che legga i dati
    public async Task<string[]> GetTitlesAsync(int take = 5)
    {
        var items = await _goalRepo.GetListAsync();
        return items.Take(take).Select(x => x.Title).ToArray();
    }
}