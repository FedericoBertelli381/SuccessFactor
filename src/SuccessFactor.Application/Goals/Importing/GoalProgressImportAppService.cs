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

public class GoalProgressImportAppService : ApplicationService
{
    private readonly IRepository<GoalProgressBatch, Guid> _batchRepo;
    private readonly IRepository<GoalProgressRow, Guid> _rowRepo;

    private readonly IRepository<Cycle, Guid> _cycleRepo;
    private readonly IRepository<Employee, Guid> _employeeRepo;
    private readonly IRepository<GoalAssignment, Guid> _assignmentRepo;
    private readonly IRepository<GoalProgressEntry, Guid> _entryRepo;

    public GoalProgressImportAppService(
        IRepository<GoalProgressBatch, Guid> batchRepo,
        IRepository<GoalProgressRow, Guid> rowRepo,
        IRepository<Cycle, Guid> cycleRepo,
        IRepository<Employee, Guid> employeeRepo,
        IRepository<GoalAssignment, Guid> assignmentRepo,
        IRepository<GoalProgressEntry, Guid> entryRepo)
    {
        _batchRepo = batchRepo;
        _rowRepo = rowRepo;
        _cycleRepo = cycleRepo;
        _employeeRepo = employeeRepo;
        _assignmentRepo = assignmentRepo;
        _entryRepo = entryRepo;
    }

    public async Task<ImportBatchDto> CreateBatchAsync(CreateImportBatchDto input)
    {
        EnsureTenant();

        if (!await _cycleRepo.AnyAsync(x => x.Id == input.CycleId))
            throw new BusinessException("CycleNotFound");

        var batch = new GoalProgressBatch
        {
            TenantId = CurrentTenant.Id,
            CycleId = input.CycleId,
            FileName = input.FileName,
            Status = "Uploaded"
        };

        await _batchRepo.InsertAsync(batch, autoSave: true);
        return ObjectMapper.Map<GoalProgressBatch, ImportBatchDto>(batch);
    }

    public async Task<int> AddRowsAsync(Guid batchId, GoalProgressImportItemDto[] items)
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

            var row = new GoalProgressRow
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
                var item = JsonSerializer.Deserialize<GoalProgressImportItemDto>(row.RawJson)
                           ?? throw new Exception("Invalid JSON");

                var assignmentId = await ResolveAssignmentIdAsync(batch.CycleId, item);
                if (!assignmentId.HasValue) throw new BusinessException("GoalAssignmentNotFound");

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
                var item = JsonSerializer.Deserialize<GoalProgressImportItemDto>(row.RawJson)
                           ?? throw new Exception("Invalid JSON");

                var assignmentId = await ResolveAssignmentIdAsync(batch.CycleId, item);
                if (!assignmentId.HasValue) throw new BusinessException("GoalAssignmentNotFound");

                // upsert progress entry (AssignmentId + EntryDate)
                var existing = await _entryRepo.FirstOrDefaultAsync(e =>
                    e.AssignmentId == assignmentId.Value && e.EntryDate == item.EntryDate);

                if (existing == null)
                {
                    var entity = new GoalProgressEntry
                    {
                        TenantId = CurrentTenant.Id,
                        AssignmentId = assignmentId.Value,
                        EntryDate = item.EntryDate,
                        ProgressPercent = item.ProgressPercent,
                        ActualValue = item.ActualValue,
                        Note = item.Note,
                        AttachmentId = item.AttachmentId
                    };
                    await _entryRepo.InsertAsync(entity, autoSave: true);
                }
                else
                {
                    existing.ProgressPercent = item.ProgressPercent;
                    existing.ActualValue = item.ActualValue;
                    existing.Note = item.Note;
                    existing.AttachmentId = item.AttachmentId;
                    await _entryRepo.UpdateAsync(existing, autoSave: true);
                }

                ok++;
            }
            catch (Exception ex)
            {
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

    private async Task<Guid?> ResolveAssignmentIdAsync(Guid batchCycleId, GoalProgressImportItemDto item)
    {
        if (item.AssignmentId.HasValue)
            return item.AssignmentId;

        // fallback: risolvi con cycle + employee matricola + goalid
        var cycleId = item.CycleId ?? batchCycleId;

        Guid? employeeId = null;
        if (!string.IsNullOrWhiteSpace(item.EmployeeMatricola))
        {
            var emp = await _employeeRepo.FirstOrDefaultAsync(e => e.Matricola == item.EmployeeMatricola);
            employeeId = emp?.Id;
        }

        if (!employeeId.HasValue || !item.GoalId.HasValue)
            return null;

        var ass = await _assignmentRepo.FirstOrDefaultAsync(a =>
            a.CycleId == cycleId && a.EmployeeId == employeeId.Value && a.GoalId == item.GoalId.Value);

        return ass?.Id;
    }

    private void EnsureTenant()
    {
        if (CurrentTenant.Id == null) throw new BusinessException("TenantMissing");
    }

    private async Task<GoalProgressBatch> GetBatchOrThrow(Guid batchId)
    {
        var batch = await _batchRepo.FirstOrDefaultAsync(b => b.Id == batchId);
        if (batch == null) throw new BusinessException("BatchNotFound");
        if (batch.TenantId != CurrentTenant.Id) throw new BusinessException("BatchTenantMismatch");
        return batch;
    }
}