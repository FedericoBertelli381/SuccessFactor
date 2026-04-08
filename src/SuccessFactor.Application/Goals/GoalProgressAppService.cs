using SuccessFactor.Workflow;
using SuccessFactor.Workflow.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;


namespace SuccessFactor.Goals;

public class GoalProgressAppService : ApplicationService, IGoalProgressAppService
{
    private readonly IRepository<GoalProgressEntry, Guid> _entryRepo;
    private readonly IRepository<GoalAssignment, Guid> _assignmentRepo;
    private readonly WorkflowAuthorizationService _wfAuth;

    public GoalProgressAppService(
        IRepository<GoalProgressEntry, Guid> entryRepo,
        IRepository<GoalAssignment, Guid> assignmentRepo,
        WorkflowAuthorizationService wfAuth)
    {
        _entryRepo = entryRepo;
        _assignmentRepo = assignmentRepo;
        _wfAuth = wfAuth;
    }

    /// <summary>
    /// Inserisce/aggiorna il progresso di un obiettivo per una specifica data.
    /// Regola: 1 riga per AssignmentId + EntryDate (upsert).
    /// </summary>
    public async Task<GoalProgressEntryDto> AddAsync(AddGoalProgressDto input)
    {
        EnsureTenant();

        // Assignment deve esistere (tenant-safe grazie a IMultiTenant + CurrentTenant)
        var assignment = await _assignmentRepo.FirstOrDefaultAsync(x => x.Id == input.AssignmentId);
        if (assignment == null)
            throw new BusinessException("GoalAssignmentNotFound");

        // opzionale: non permettere progress su assignment chiusi
        if (string.Equals(assignment.Status, "Closed", StringComparison.OrdinalIgnoreCase))
            throw new BusinessException("GoalAssignmentClosed");

        // EntryDate dentro finestra assegnazione (se valorizzata)
        if (assignment.StartDate.HasValue && input.EntryDate < assignment.StartDate.Value)
            throw new BusinessException("EntryDateBeforeAssignmentStart");

        if (assignment.DueDate.HasValue && input.EntryDate > assignment.DueDate.Value)
            throw new BusinessException("EntryDateAfterAssignmentDue");
        // Almeno un dato utile
        if (input.ProgressPercent is null && input.ActualValue is null && string.IsNullOrWhiteSpace(input.Note))
            throw new BusinessException("ProgressEntryEmpty");
        // Se è il primo progress dell'assignment e lo stato è Draft -> InProgress
        if (string.Equals(assignment.Status, "Draft", StringComparison.OrdinalIgnoreCase))
        {
            var anyExisting = await _entryRepo.AnyAsync(e => e.AssignmentId == input.AssignmentId);
            if (!anyExisting)
            {
                assignment.Status = "InProgress";
                await _assignmentRepo.UpdateAsync(assignment, autoSave: true);
            }
        }
        var auth = await _wfAuth.EvaluateAsync(assignment.CycleId, assignment.EmployeeId);
        auth.EnsureCanEdit();

        // Se il client sta valorizzando un campo, deve essere Editabile in fase
        if (input.ProgressPercent.HasValue)
            auth.EnsureFieldEditable(FieldKeys.Goals_ProgressPercent);

        if (input.ActualValue.HasValue)
            auth.EnsureFieldEditable(FieldKeys.Goals_ActualValue);

        if (input.Note != null) // stringa presente (anche vuota) = update
            auth.EnsureFieldEditable(FieldKeys.Goals_Note);

        if (input.AttachmentId.HasValue)
            auth.EnsureFieldEditable(FieldKeys.Goals_Attachment);
        // Upsert: se esiste riga per (AssignmentId, EntryDate) aggiorno, altrimenti inserisco
        var existing = await _entryRepo.FirstOrDefaultAsync(x =>
            x.AssignmentId == input.AssignmentId &&
            x.EntryDate == input.EntryDate);

        if (existing == null)
        {
            var entity = ObjectMapper.Map<AddGoalProgressDto, GoalProgressEntry>(input);
            entity.TenantId = CurrentTenant.Id;

            await _entryRepo.InsertAsync(entity, autoSave: true);
            return ObjectMapper.Map<GoalProgressEntry, GoalProgressEntryDto>(entity);
        }
        else
        {
            ObjectMapper.Map(input, existing); // aggiorna i campi
            await _entryRepo.UpdateAsync(existing, autoSave: true);
            return ObjectMapper.Map<GoalProgressEntry, GoalProgressEntryDto>(existing);
        }
    }
    public async Task<List<GoalProgressEntryDto>> GetByAssignmentAsync(Guid assignmentId)
    {
        EnsureTenant();

        // opzionale ma consigliato: se assignment non esiste -> errore chiaro
        var assignmentExists = await _assignmentRepo.AnyAsync(x => x.Id == assignmentId);
        if (!assignmentExists)
            throw new BusinessException("GoalAssignmentNotFound");

        var q = await _entryRepo.GetQueryableAsync();
        var items = await AsyncExecuter.ToListAsync(
            q.Where(x => x.AssignmentId == assignmentId)
             .OrderBy(x => x.EntryDate)
        );

        var dtos = items.Select(ObjectMapper.Map<GoalProgressEntry, GoalProgressEntryDto>).ToList();
        return new List<GoalProgressEntryDto>(dtos);
    }
    public async Task<GoalProgressEntryDto?> GetLastProgressAsync(Guid assignmentId)
    {
        EnsureTenant();

        var assignmentExists = await _assignmentRepo.AnyAsync(x => x.Id == assignmentId);
        if (!assignmentExists)
            throw new BusinessException("GoalAssignmentNotFound");

        var q = await _entryRepo.GetQueryableAsync();

        var last = await AsyncExecuter.FirstOrDefaultAsync(
            q.Where(x => x.AssignmentId == assignmentId)
             .OrderByDescending(x => x.EntryDate)
        );

        return last == null ? null : ObjectMapper.Map<GoalProgressEntry, GoalProgressEntryDto>(last);
    }

    public async Task<GoalProgressSummaryDto> GetProgressSummaryAsync(Guid assignmentId)
    {
        EnsureTenant();

        var assignmentExists = await _assignmentRepo.AnyAsync(x => x.Id == assignmentId);
        if (!assignmentExists)
            throw new BusinessException("GoalAssignmentNotFound");

        var q = await _entryRepo.GetQueryableAsync();

        var items = await AsyncExecuter.ToListAsync(
            q.Where(x => x.AssignmentId == assignmentId)
             .OrderByDescending(x => x.EntryDate)
        );

        if (items.Count == 0)
        {
            return new GoalProgressSummaryDto
            {
                AssignmentId = assignmentId,
                EntriesCount = 0
            };
        }

        var last = items[0];

        return new GoalProgressSummaryDto
        {
            AssignmentId = assignmentId,
            EntriesCount = items.Count,
            LastEntryDate = last.EntryDate,
            LastProgressPercent = last.ProgressPercent,
            LastActualValue = last.ActualValue,
            LastNote = last.Note
        };
    }
    private void EnsureTenant()
    {
        if (CurrentTenant.Id == null)
            throw new BusinessException("TenantMissing")
                .WithData("Hint", "Aggiungi ?__tenant=NOME_TENANT alla chiamata.");
    }
}