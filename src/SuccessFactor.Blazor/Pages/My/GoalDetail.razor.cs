using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using SuccessFactor.My;
using SuccessFactor.My.Dtos;
using SuccessFactor.Goals;

namespace SuccessFactor.Blazor.Pages.My;

public class GoalDetailBase : ComponentBase
{
    [Parameter]
    public Guid AssignmentId { get; set; }

    [Inject]
    protected NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    protected IMyGoalsAppService MyGoalsAppService { get; set; } = default!;

    [Inject]
    protected IGoalProgressAppService GoalProgressAppService { get; set; } = default!;

    [Inject]
    protected IMyContextAppService MyContextAppService { get; set; } = default!;

    protected bool Loading { get; set; }
    protected bool Saving { get; set; }
    protected string? Error { get; set; }
    protected string? SuccessMessage { get; set; }

    protected MyGoalsDto? GoalsData { get; set; }
    protected MyGoalItemDto? GoalItem { get; set; }

    protected List<GoalProgressEntryDto> History { get; set; } = [];
    protected GoalProgressEntryDto? LastProgress { get; set; }
    protected GoalProgressSummaryDto? Summary { get; set; }

    protected AddProgressFormModel Input { get; set; } = new();

    protected int SummaryEntriesCount => Summary?.EntriesCount ?? GoalItem?.Summary?.EntriesCount ?? 0;
    protected DateOnly? SummaryLastEntryDate => Summary?.LastEntryDate ?? GoalItem?.Summary?.LastEntryDate;
    protected decimal? SummaryLastProgressPercent => Summary?.LastProgressPercent ?? GoalItem?.Summary?.LastProgressPercent;
    protected decimal? SummaryLastActualValue => Summary?.LastActualValue ?? GoalItem?.Summary?.LastActualValue;

    protected override async Task OnInitializedAsync()
    {
        Input = new AddProgressFormModel
        {
            EntryDate = DateOnly.FromDateTime(DateTime.Today)
        };

        await LoadAsync();
    }

    protected async Task ReloadAsync()
    {
        await LoadAsync();
    }

    protected async Task LoadAsync()
    {
        Loading = true;
        Error = null;
        SuccessMessage = null;
        GoalsData = null;
        GoalItem = null;
        History = [];
        LastProgress = null;
        Summary = null;

        try
        {
            var contextStatus = await MyContextAppService.GetStatusAsync();

            if (!contextStatus.IsReady)
            {
                Error = contextStatus.ErrorMessage
                    ?? Components.Shared.UiErrorMessage.FromCode(contextStatus.ErrorCode)
                    ?? contextStatus.ErrorCode
                    ?? "Area My non disponibile.";
                return;
            }

            GoalsData = await MyGoalsAppService.GetAsync(new GetMyGoalsInput());

            GoalItem = GoalsData.Items.FirstOrDefault(x => x.AssignmentId == AssignmentId);

            if (GoalItem is null)
            {
                Error = "Goal non trovato tra i goal dell'utente corrente.";
                return;
            }

            History = (await GoalProgressAppService.GetByAssignmentAsync(AssignmentId))
                .OrderByDescending(x => x.EntryDate)
                .ToList();

            LastProgress = await GoalProgressAppService.GetLastProgressAsync(AssignmentId);
            Summary = await GoalProgressAppService.GetProgressSummaryAsync(AssignmentId);
        }
        catch (Exception ex)
        {
            Error = Components.Shared.UiErrorMessage.From(ex);
            GoalsData = null;
            GoalItem = null;
            History = [];
            LastProgress = null;
            Summary = null;
        }
        finally
        {
            Loading = false;
        }
    }

    protected async Task SaveAsync()
    {
        if (GoalItem is null)
        {
            return;
        }

        Saving = true;
        Error = null;
        SuccessMessage = null;

        try
        {
            var input = new AddGoalProgressDto
            {
                AssignmentId = GoalItem.AssignmentId,
                EntryDate = Input.EntryDate,
                ProgressPercent = Input.ProgressPercent,
                ActualValue = Input.ActualValue,
                Note = Input.Note
            };

            await GoalProgressAppService.AddAsync(input);

            SuccessMessage = "Aggiornamento salvato correttamente.";

            Input = new AddProgressFormModel
            {
                EntryDate = DateOnly.FromDateTime(DateTime.Today)
            };

            await LoadAsync();
        }
        catch (Exception ex)
        {
            Error = Components.Shared.UiErrorMessage.From(ex);
        }
        finally
        {
            Saving = false;
        }
    }

    protected void BackToMyPage()
    {
        var url = "/my";
        var tenant = GetCurrentTenantFromUrl();

        if (!string.IsNullOrWhiteSpace(tenant))
        {
            url = QueryHelpers.AddQueryString(url, "__tenant", tenant);
        }

        NavigationManager.NavigateTo(url);
    }

    protected string? GetCurrentTenantFromUrl()
    {
        var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
        var query = QueryHelpers.ParseQuery(uri.Query);

        if (query.TryGetValue("__tenant", out var tenantValues))
        {
            var tenant = tenantValues.ToString();
            return string.IsNullOrWhiteSpace(tenant) ? null : tenant;
        }

        return null;
    }

    protected bool CanEditAnyGoalField()
    {
        if (GoalItem is null)
        {
            return false;
        }

        return IsEditAccess(GoalItem.ProgressPercentAccess)
            || IsEditAccess(GoalItem.ActualValueAccess)
            || IsEditAccess(GoalItem.NoteAccess)
            || IsEditAccess(GoalItem.AttachmentAccess);
    }

    protected static bool IsEditAccess(string? access)
        => string.Equals(access, "Edit", StringComparison.OrdinalIgnoreCase);

    protected static string YesNo(bool value)
        => value ? "Sì" : "No";

    protected static string FormatDate(DateOnly? value)
        => value?.ToString("dd/MM/yyyy") ?? "-";

    protected static string FormatDecimal(decimal? value)
        => value?.ToString("0.##") ?? "-";

    public class AddProgressFormModel
    {
        [Required]
        public DateOnly EntryDate { get; set; }

        [Range(0, 100)]
        public decimal? ProgressPercent { get; set; }

        public decimal? ActualValue { get; set; }

        [StringLength(4000)]
        public string? Note { get; set; }
    }
}
