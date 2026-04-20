using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using SuccessFactor.Competencies;
using SuccessFactor.Competencies.Assessments;
using SuccessFactor.My;
using SuccessFactor.My.Dtos;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace SuccessFactor.Blazor.Pages.My;

public class AssessmentDetailBase : ComponentBase
{
    [Parameter]
    public Guid AssessmentId { get; set; }

    [Inject]
    protected NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    protected IMyAssessmentsAppService MyAssessmentsAppService { get; set; } = default!;
    [Inject]
    protected IJSRuntime JSRuntime { get; set; } = default!;

    [Inject]
    protected IMyContextAppService MyContextAppService { get; set; } = default!;

    protected bool Loading { get; set; }
    protected bool Submitting { get; set; }
    
    protected string? Error { get; set; }
    protected string? SuccessMessage { get; set; }

    protected MyAssessmentsDto? AssessmentsData { get; set; }
    protected MyAssessmentItemDto? AssessmentSummary { get; set; }


    protected List<EditableAssessmentItemVm> EditableItems { get; set; } = [];

    protected override async Task OnInitializedAsync()
    {
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
        AssessmentsData = null;
        AssessmentSummary = null;
        EditableItems = [];

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

            AssessmentsData = await MyAssessmentsAppService.GetAsync(new GetMyAssessmentsInput());

            AssessmentSummary = AssessmentsData.Items.FirstOrDefault(x => x.AssessmentId == AssessmentId);

            if (AssessmentSummary is null)
            {
                Error = "Assessment non trovato tra gli assessment dell'utente corrente.";
                return;
            }

            EditableItems = AssessmentSummary.Items
                .Select(x => new EditableAssessmentItemVm
                {
                    CompetencyId = x.CompetencyId,
                    CompetencyCode = x.CompetencyCode,
                    CompetencyName = x.CompetencyName,
                    IsRequired = x.IsRequired,
                    Weight = x.Weight,
                    Score = x.Score,
                    Comment = x.Comment
                })
                .OrderBy(x => x.CompetencyCode)
                .ToList();
        }
        catch (Exception ex)
        {
            Error = Components.Shared.UiErrorMessage.From(ex);
            AssessmentsData = null;
            AssessmentSummary = null;
            EditableItems = [];
        }
        finally
        {
            Loading = false;
        }
    }

    protected async Task SaveItemAsync(EditableAssessmentItemVm item)
    {
        if (AssessmentSummary is null || !AssessmentSummary.CanEdit)
        {
            return;
        }

        item.Saving = true;
        Error = null;
        SuccessMessage = null;

        try
        {
            var input = new UpsertAssessmentItemDto
            {
                AssessmentId = AssessmentSummary.AssessmentId,
                CompetencyId = item.CompetencyId,
                Score = item.Score,
                Comment = item.Comment
            };

            await MyAssessmentsAppService.UpsertItemAsync(input);

            await LoadAsync();

            SuccessMessage = $"Competenza '{item.CompetencyCode}' salvata correttamente.";
        }
        catch (Exception ex)
        {
            Error = Components.Shared.UiErrorMessage.From(ex);
        }
        finally
        {
            item.Saving = false;
        }
    }

    protected async Task SubmitAssessmentAsync()
    {
        if (AssessmentSummary is null || !AssessmentSummary.CanSubmit)
        {
            return;
        }

        var confirmed = await JSRuntime.InvokeAsync<bool>(
            "confirm",
            "Confermi l'invio dell'assessment? Dopo il submit non potrai più modificarlo.");

        if (!confirmed)
        {
            return;
        }

        Submitting = true;
        Error = null;
        SuccessMessage = null;

        try
        {
            await MyAssessmentsAppService.SubmitAsync(AssessmentSummary.AssessmentId);

            await LoadAsync();

            SuccessMessage = "Assessment inviato correttamente.";
        }
        catch (Exception ex)
        {
            Error = Components.Shared.UiErrorMessage.From(ex);
        }
        finally
        {
            Submitting = false;
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

    protected bool CanEditScore()
        => AssessmentSummary is not null
           && AssessmentSummary.CanEdit
           && string.Equals(AssessmentSummary.ScoreAccess, "Edit", StringComparison.OrdinalIgnoreCase);

    protected bool CanEditComment()
        => AssessmentSummary is not null
           && AssessmentSummary.CanEdit
           && string.Equals(AssessmentSummary.CommentAccess, "Edit", StringComparison.OrdinalIgnoreCase);

    protected static string YesNo(bool value)
        => value ? "Sì" : "No";

    protected static string FormatDecimal(decimal? value)
        => value?.ToString("0.##") ?? "-";

    protected static string FormatInt(int? value)
        => value?.ToString() ?? "-";

    public class EditableAssessmentItemVm
    {
        public Guid CompetencyId { get; set; }

        public string CompetencyCode { get; set; } = string.Empty;

        public string CompetencyName { get; set; } = string.Empty;

        public bool IsRequired { get; set; }

        public decimal? Weight { get; set; }

        [Range(1, 5)]
        public int? Score { get; set; }

        [StringLength(2000)]
        public string? Comment { get; set; }

        public bool Saving { get; set; }
    }

}
