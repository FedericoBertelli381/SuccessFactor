using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using SuccessFactor.Cycles;
using SuccessFactor.Employees;
using SuccessFactor.Goals;

namespace SuccessFactor.Goals.Importing;

public class GoalImportAppService : ApplicationService
{
    private readonly IRepository<GoalImportBatch, Guid> _batchRepo;
    private readonly IRepository<GoalImportRow, Guid> _rowRepo;

    private readonly IRepository<Cycle, Guid> _cycleRepo;
    private readonly IRepository<Employee, Guid> _employeeRepo;
    private readonly IRepository<Goal, Guid> _goalRepo;
    private readonly IRepository<GoalAssignment, Guid> _assignmentRepo;

    public GoalImportAppService(
        IRepository<GoalImportBatch, Guid> batchRepo,
        IRepository<GoalImportRow, Guid> rowRepo,
        IRepository<Cycle, Guid> cycleRepo,
        IRepository<Employee, Guid> employeeRepo,
        IRepository<Goal, Guid> goalRepo,
        IRepository<GoalAssignment, Guid> assignmentRepo)
    {
        _batchRepo = batchRepo;
        _rowRepo = rowRepo;
        _cycleRepo = cycleRepo;
        _employeeRepo = employeeRepo;
        _goalRepo = goalRepo;
        _assignmentRepo = assignmentRepo;
    }

    public async Task<ImportBatchDto> CreateBatchAsync(CreateImportBatchDto input)
    {
        EnsureTenant();

        if (!await _cycleRepo.AnyAsync(x => x.Id == input.CycleId))
            throw new BusinessException("CycleNotFound");

        var batch = new GoalImportBatch
        {
            TenantId = CurrentTenant.Id,
            CycleId = input.CycleId,
            FileName = input.FileName,
            Status = "Uploaded"
        };

        await _batchRepo.InsertAsync(batch, autoSave: true);
        return ObjectMapper.Map<GoalImportBatch, ImportBatchDto>(batch);
    }

    public async Task<int> AddRowsAsync(Guid batchId, GoalAssignmentImportItemDto[] items)
    {
        EnsureTenant();
        var batch = await GetBatchOrThrow(batchId);

        var q = await _rowRepo.GetQueryableAsync();

        var lastRowNumber = await AsyncExecuter.MaxAsync(
            q.Where(r => r.BatchId == batchId)
             .Select(r => (int?)r.RowNumber)
        ) ?? 0;

        var startRow = lastRowNumber + 1;

        foreach (var (item, idx) in items.Select((x, i) => (x, i)))
        {
            if (item.RowNumber <= 0) item.RowNumber = (int)startRow + idx;

            var row = new GoalImportRow
            {
                BatchId = batchId,
                RowNumber = item.RowNumber,
                RawJson = JsonSerializer.Serialize(item),
                ValidationStatus = "Pending"
            };
            await _rowRepo.InsertAsync(row, autoSave: true);
        }

        return items.Length;
    }

    public async Task<BatchResultDto> ValidateBatchAsync(Guid batchId)
    {
        EnsureTenant();
        var batch = await GetBatchOrThrow(batchId);

        var rows = await _rowRepo.GetListAsync(r => r.BatchId == batchId);

        int ok = 0, err = 0;

        foreach (var row in rows.OrderBy(r => r.RowNumber))
        {
            try
            {
                var item = JsonSerializer.Deserialize<GoalAssignmentImportItemDto>(row.RawJson)
                           ?? throw new Exception("Invalid JSON");

                // 1) resolve employee
                var employeeId = item.EmployeeId;
                if (!employeeId.HasValue && !string.IsNullOrWhiteSpace(item.EmployeeMatricola))
                {
                    var emp = await _employeeRepo.FirstOrDefaultAsync(e => e.Matricola == item.EmployeeMatricola);
                    employeeId = emp?.Id;
                }
                if (!employeeId.HasValue) throw new BusinessException("EmployeeNotFound");

                // 2) resolve/create goal
                Guid? goalId = item.GoalId;
                if (!goalId.HasValue && !string.IsNullOrWhiteSpace(item.GoalTitle))
                {
                    var existing = await _goalRepo.FirstOrDefaultAsync(g => g.Title == item.GoalTitle);
                    if (existing != null) goalId = existing.Id;
                }
                if (!goalId.HasValue && string.IsNullOrWhiteSpace(item.GoalTitle))
                    throw new BusinessException("GoalMissing");

                // 3) date coherence
                if (item.StartDate.HasValue && item.DueDate.HasValue && item.StartDate.Value > item.DueDate.Value)
                    throw new BusinessException("StartDateAfterDueDate");

                // 4) duplicate (same cycle+employee+goal) — if goalId exists now
                if (goalId.HasValue)
                {
                    var dup = await _assignmentRepo.AnyAsync(a =>
                        a.CycleId == batch.CycleId && a.EmployeeId == employeeId.Value && a.GoalId == goalId.Value);

                    if (dup) throw new BusinessException("GoalAlreadyAssignedToEmployee");
                }

                row.ValidationStatus = "Ok";
                row.ErrorMessage = null;
                ok++;
            }
            catch (Exception ex)
            {
                row.ValidationStatus = "Error";
                row.ErrorMessage = ex.Message;
                err++;
            }

            await _rowRepo.UpdateAsync(row, autoSave: true);
        }

        batch.Status = "Validated";
        await _batchRepo.UpdateAsync(batch, autoSave: true);

        return new BatchResultDto { Total = rows.Count, Ok = ok, Error = err, Status = batch.Status };
    }

    public async Task<BatchResultDto> CommitBatchAsync(Guid batchId)
    {
        EnsureTenant();
        var batch = await GetBatchOrThrow(batchId);

        if (!string.Equals(batch.Status, "Validated", StringComparison.OrdinalIgnoreCase))
            throw new BusinessException("BatchNotValidated");

        var rows = await _rowRepo.GetListAsync(r => r.BatchId == batchId && r.ValidationStatus == "Ok");

        int ok = 0, err = 0;

        foreach (var row in rows.OrderBy(r => r.RowNumber))
        {
            try
            {
                var item = JsonSerializer.Deserialize<GoalAssignmentImportItemDto>(row.RawJson)
                           ?? throw new Exception("Invalid JSON");

                // resolve employee
                var employeeId = item.EmployeeId;
                if (!employeeId.HasValue && !string.IsNullOrWhiteSpace(item.EmployeeMatricola))
                {
                    var emp = await _employeeRepo.FirstOrDefaultAsync(e => e.Matricola == item.EmployeeMatricola);
                    employeeId = emp?.Id;
                }
                if (!employeeId.HasValue) throw new BusinessException("EmployeeNotFound");

                // resolve or create goal
                Goal goal;
                if (item.GoalId.HasValue)
                {
                    goal = await _goalRepo.GetAsync(item.GoalId.Value);
                }
                else
                {
                    var existing = await _goalRepo.FirstOrDefaultAsync(g => g.Title == item.GoalTitle);
                    if (existing != null) goal = existing;
                    else
                    {
                        goal = new Goal
                        {
                            TenantId = CurrentTenant.Id,
                            Title = item.GoalTitle ?? throw new BusinessException("GoalMissing"),
                            Description = item.GoalDescription,
                            Category = item.GoalCategory,
                            IsLibraryItem = item.IsLibraryItem ?? false
                        };
                        await _goalRepo.InsertAsync(goal, autoSave: true);
                    }
                }

                // insert assignment
                var assignment = new GoalAssignment
                {
                    TenantId = CurrentTenant.Id,
                    CycleId = batch.CycleId,
                    EmployeeId = employeeId.Value,
                    GoalId = goal.Id,
                    Weight = item.Weight,
                    TargetValue = item.TargetValue,
                    StartDate = item.StartDate,
                    DueDate = item.DueDate,
                    Status = item.Status
                };

                await _assignmentRepo.InsertAsync(assignment, autoSave: true);
                ok++;
            }
            catch (Exception ex)
            {
                // se fallisce commit, marchiamo la riga come error
                row.ValidationStatus = "Error";
                row.ErrorMessage = "COMMIT: " + ex.Message;
                await _rowRepo.UpdateAsync(row, autoSave: true);
                err++;
            }
        }

        batch.Status = err == 0 ? "Committed" : "Failed";
        await _batchRepo.UpdateAsync(batch, autoSave: true);

        return new BatchResultDto { Total = rows.Count, Ok = ok, Error = err, Status = batch.Status };
    }

    private void EnsureTenant()
    {
        if (CurrentTenant.Id == null) throw new BusinessException("TenantMissing");
    }

    private async Task<GoalImportBatch> GetBatchOrThrow(Guid batchId)
    {
        var batch = await _batchRepo.FirstOrDefaultAsync(b => b.Id == batchId);
        if (batch == null) throw new BusinessException("BatchNotFound");
        if (batch.TenantId != CurrentTenant.Id) throw new BusinessException("BatchTenantMismatch");
        return batch;
    }
}