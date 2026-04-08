using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using SuccessFactor.Cycles;
using SuccessFactor.Employees;

namespace SuccessFactor.Goals;

public class GoalAssignmentAppService
    : CrudAppService<GoalAssignment, GoalAssignmentDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateGoalAssignmentDto>
{
    private readonly IRepository<Goal, Guid> _goalRepo;
    private readonly IRepository<Employee, Guid> _employeeRepo;
    private readonly IRepository<Cycle, Guid> _cycleRepo;

    public GoalAssignmentAppService(
        IRepository<GoalAssignment, Guid> repository,
        IRepository<Goal, Guid> goalRepo,
        IRepository<Employee, Guid> employeeRepo,
        IRepository<Cycle, Guid> cycleRepo)
        : base(repository)
    {
        _goalRepo = goalRepo;
        _employeeRepo = employeeRepo;
        _cycleRepo = cycleRepo;
    }

    public override async Task<GoalAssignmentDto> CreateAsync(CreateUpdateGoalAssignmentDto input)
    {
        EnsureTenant();

        await ValidateRefsAsync(input.CycleId, input.EmployeeId, input.GoalId);
        await ValidateBusinessRulesAsync(null, input);

        return await base.CreateAsync(input);
    }

    public override async Task<GoalAssignmentDto> UpdateAsync(Guid id, CreateUpdateGoalAssignmentDto input)
    {
        EnsureTenant();

        await ValidateRefsAsync(input.CycleId, input.EmployeeId, input.GoalId);
        await ValidateBusinessRulesAsync(id, input);

        return await base.UpdateAsync(id, input);
    }

    private void EnsureTenant()
    {
        if (CurrentTenant.Id == null)
            throw new BusinessException("TenantMissing");
    }

    private async Task ValidateRefsAsync(Guid cycleId, Guid employeeId, Guid goalId)
    {
        if (!await _cycleRepo.AnyAsync(x => x.Id == cycleId))
            throw new BusinessException("CycleNotFound");

        if (!await _employeeRepo.AnyAsync(x => x.Id == employeeId))
            throw new BusinessException("EmployeeNotFound");

        if (!await _goalRepo.AnyAsync(x => x.Id == goalId))
            throw new BusinessException("GoalNotFound");
    }

    private async Task ValidateBusinessRulesAsync(Guid? updatingId, CreateUpdateGoalAssignmentDto input)
    {
        // (1) Start/Due coerenti
        if (input.StartDate.HasValue && input.DueDate.HasValue && input.StartDate.Value > input.DueDate.Value)
            throw new BusinessException("StartDateAfterDueDate");

        // (2) No duplicato stesso goal per stesso employee nello stesso ciclo (solo attivo)
        var dup = await Repository.AnyAsync(x =>
            x.CycleId == input.CycleId &&
            x.EmployeeId == input.EmployeeId &&
            x.GoalId == input.GoalId &&
            (!updatingId.HasValue || x.Id != updatingId.Value));

        if (dup) throw new BusinessException("GoalAlreadyAssignedToEmployee");

        // (3) Somma pesi <= 100 per employee+ciclo
        var current = await Repository.GetListAsync(x =>
            x.CycleId == input.CycleId &&
            x.EmployeeId == input.EmployeeId);

        var sumOther = current
            .Where(x => !updatingId.HasValue || x.Id != updatingId.Value)
            .Sum(x => x.Weight);

        if (sumOther + input.Weight > 100m)
            throw new BusinessException("TotalWeightExceeds100")
                .WithData("Current", sumOther)
                .WithData("NewWeight", input.Weight);
    }
}