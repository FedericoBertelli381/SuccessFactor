using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SuccessFactor.Competencies;
using SuccessFactor.Security;
using SuccessFactor.Competencies.Assessments;
using SuccessFactor.Competencies.Models;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Users;

namespace SuccessFactor.Admin;

[Authorize]
public class AdminCompetencyModelAppService : ApplicationService, IAdminCompetencyModelAppService
{
    private readonly ICurrentUser _currentUser;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IRepository<CompetencyModel, Guid> _modelRepository;
    private readonly IRepository<CompetencyModelItem, Guid> _modelItemRepository;
    private readonly IRepository<Competency, Guid> _competencyRepository;
    private readonly IRepository<CompetencyAssessment, Guid> _assessmentRepository;

    public AdminCompetencyModelAppService(
        ICurrentUser currentUser,
        IAsyncQueryableExecuter asyncExecuter,
        IRepository<CompetencyModel, Guid> modelRepository,
        IRepository<CompetencyModelItem, Guid> modelItemRepository,
        IRepository<Competency, Guid> competencyRepository,
        IRepository<CompetencyAssessment, Guid> assessmentRepository)
    {
        _currentUser = currentUser;
        _asyncExecuter = asyncExecuter;
        _modelRepository = modelRepository;
        _modelItemRepository = modelItemRepository;
        _competencyRepository = competencyRepository;
        _assessmentRepository = assessmentRepository;
    }

    public async Task<CompetencyModelAdminDto> GetAsync(Guid? modelId = null)
    {
        EnsureTenantAndAdmin();

        var modelQuery = await _modelRepository.GetQueryableAsync();
        var models = await _asyncExecuter.ToListAsync(modelQuery.OrderBy(x => x.Name));
        var selectedModel = ResolveSelectedModel(models, modelId);

        var modelItems = await _modelItemRepository.GetListAsync();
        var assessments = await _assessmentRepository.GetListAsync(x => x.ModelId.HasValue);
        var competencies = await _competencyRepository.GetListAsync();
        var competencyById = competencies.ToDictionary(x => x.Id, x => x);

        var selectedItems = selectedModel is null
            ? new List<CompetencyModelItem>()
            : modelItems.Where(x => x.ModelId == selectedModel.Id).ToList();

        var selectedModelAssessmentCount = selectedModel is null
            ? 0
            : assessments.Count(x => x.ModelId == selectedModel.Id);

        return new CompetencyModelAdminDto
        {
            SelectedModelId = selectedModel?.Id,
            SelectedModelName = selectedModel?.Name,
            SelectedModelAssessmentCount = selectedModelAssessmentCount,
            CanEditSelectedModel = selectedModel is not null && selectedModelAssessmentCount == 0,
            Models = MapModels(models, modelItems, assessments),
            Competencies = MapCompetencies(competencies, modelItems),
            Items = selectedItems
                .Select(x => MapItem(x, competencyById))
                .OrderBy(x => x.CompetencyCode)
                .ThenBy(x => x.CompetencyName)
                .ToList()
        };
    }

    public async Task<CompetencyModelAdminListItemDto> SaveModelAsync(Guid? modelId, SaveCompetencyModelInput input)
    {
        EnsureTenantAndAdmin();
        NormalizeAndValidateModel(input);

        var models = await _modelRepository.GetListAsync();
        EnsureNoDuplicateName(modelId, input.Name, models);

        CompetencyModel entity;
        if (modelId.HasValue)
        {
            entity = await _modelRepository.GetAsync(modelId.Value);
            await EnsureModelStructureEditableAsync(entity.Id);
        }
        else
        {
            entity = new CompetencyModel
            {
                TenantId = CurrentTenant.Id
            };
        }

        entity.Name = input.Name;
        entity.ScaleType = input.ScaleType;
        entity.MinScore = input.MinScore;
        entity.MaxScore = input.MaxScore;

        entity = modelId.HasValue
            ? await _modelRepository.UpdateAsync(entity, autoSave: true)
            : await _modelRepository.InsertAsync(entity, autoSave: true);

        var refreshedModels = await _modelRepository.GetListAsync();
        var modelItems = await _modelItemRepository.GetListAsync();
        var assessments = await _assessmentRepository.GetListAsync(x => x.ModelId.HasValue);

        return MapModels(refreshedModels, modelItems, assessments)
            .Single(x => x.ModelId == entity.Id);
    }

    public async Task DeleteModelAsync(Guid modelId)
    {
        EnsureTenantAndAdmin();

        if (modelId == Guid.Empty)
        {
            throw new BusinessException("CompetencyModelIdRequired");
        }

        await EnsureModelStructureEditableAsync(modelId);

        if (await _modelItemRepository.AnyAsync(x => x.ModelId == modelId))
        {
            throw new BusinessException("CompetencyModelHasItems");
        }

        await _modelRepository.DeleteAsync(modelId);
    }

    public async Task<CompetencyModelItemAdminListItemDto> SaveItemAsync(Guid? modelItemId, SaveCompetencyModelItemInput input)
    {
        EnsureTenantAndAdmin();
        NormalizeAndValidateItem(input);
        await EnsureModelStructureEditableAsync(input.ModelId);

        if (!await _competencyRepository.AnyAsync(x => x.Id == input.CompetencyId && x.IsActive))
        {
            throw new BusinessException("CompetencyNotFoundOrInactive");
        }

        if (await _modelItemRepository.AnyAsync(x =>
                x.ModelId == input.ModelId &&
                x.CompetencyId == input.CompetencyId &&
                (!modelItemId.HasValue || x.Id != modelItemId.Value)))
        {
            throw new BusinessException("CompetencyModelItemAlreadyExists");
        }

        await EnsureTotalWeightIsValidAsync(modelItemId, input);

        CompetencyModelItem entity;
        if (modelItemId.HasValue)
        {
            entity = await _modelItemRepository.GetAsync(modelItemId.Value);

            if (entity.ModelId != input.ModelId)
            {
                throw new BusinessException("CompetencyModelItemModelMismatch");
            }
        }
        else
        {
            entity = new CompetencyModelItem
            {
                TenantId = CurrentTenant.Id,
                ModelId = input.ModelId
            };
        }

        entity.CompetencyId = input.CompetencyId;
        entity.Weight = input.Weight;
        entity.IsRequired = input.IsRequired;

        entity = modelItemId.HasValue
            ? await _modelItemRepository.UpdateAsync(entity, autoSave: true)
            : await _modelItemRepository.InsertAsync(entity, autoSave: true);

        var competency = await _competencyRepository.GetAsync(entity.CompetencyId);
        return MapItem(entity, new Dictionary<Guid, Competency> { [competency.Id] = competency });
    }

    public async Task DeleteItemAsync(Guid modelItemId)
    {
        EnsureTenantAndAdmin();

        var modelItem = await _modelItemRepository.GetAsync(modelItemId);
        await EnsureModelStructureEditableAsync(modelItem.ModelId);

        await _modelItemRepository.DeleteAsync(modelItem, autoSave: true);
    }

    private void EnsureTenantAndAdmin()
    {
        if (CurrentTenant.Id is null)
        {
            throw new BusinessException("TenantMissing");
        }

        var roles = _currentUser.Roles ?? Array.Empty<string>();

        if (!SuccessFactorRoles.IsAdmin(roles))
        {
            throw new BusinessException("CurrentUserIsNotAdmin");
        }
    }

    private async Task EnsureModelStructureEditableAsync(Guid modelId)
    {
        if (await _assessmentRepository.AnyAsync(x => x.ModelId == modelId))
        {
            throw new BusinessException("CompetencyModelIsUsedByAssessments");
        }
    }

    private async Task EnsureTotalWeightIsValidAsync(Guid? modelItemId, SaveCompetencyModelItemInput input)
    {
        var items = await _modelItemRepository.GetListAsync(x => x.ModelId == input.ModelId);
        var sumOther = items
            .Where(x => !modelItemId.HasValue || x.Id != modelItemId.Value)
            .Sum(x => x.Weight ?? 0m);

        if (sumOther + (input.Weight ?? 0m) > 100m)
        {
            throw new BusinessException("CompetencyModelTotalWeightExceeds100");
        }
    }

    private static CompetencyModel? ResolveSelectedModel(List<CompetencyModel> models, Guid? modelId)
    {
        if (models.Count == 0)
        {
            return null;
        }

        if (modelId.HasValue)
        {
            var selected = models.FirstOrDefault(x => x.Id == modelId.Value);

            if (selected is null)
            {
                throw new BusinessException("CompetencyModelNotFound");
            }

            return selected;
        }

        return models.OrderBy(x => x.Name).First();
    }

    private static void NormalizeAndValidateModel(SaveCompetencyModelInput input)
    {
        input.Name = NormalizeRequired(input.Name, "Name");
        input.ScaleType = NormalizeRequired(input.ScaleType, "ScaleType");

        if (input.MinScore > input.MaxScore)
        {
            throw new BusinessException("MinScoreGreaterThanMaxScore");
        }
    }

    private static void NormalizeAndValidateItem(SaveCompetencyModelItemInput input)
    {
        if (input.ModelId == Guid.Empty)
        {
            throw new BusinessException("CompetencyModelIdRequired");
        }

        if (input.CompetencyId == Guid.Empty)
        {
            throw new BusinessException("CompetencyIdRequired");
        }

        if (input.Weight is < 0 or > 100)
        {
            throw new BusinessException("CompetencyModelItemWeightOutOfRange");
        }
    }

    private static void EnsureNoDuplicateName(Guid? modelId, string name, List<CompetencyModel> models)
    {
        if (models.Any(x =>
            string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase) &&
            (!modelId.HasValue || x.Id != modelId.Value)))
        {
            throw new BusinessException("CompetencyModelNameAlreadyExists");
        }
    }

    private static List<CompetencyModelAdminListItemDto> MapModels(
        List<CompetencyModel> models,
        List<CompetencyModelItem> modelItems,
        List<CompetencyAssessment> assessments)
    {
        var itemGroups = modelItems.GroupBy(x => x.ModelId).ToDictionary(x => x.Key, x => x.ToList());
        var assessmentCountByModelId = assessments
            .Where(x => x.ModelId.HasValue)
            .GroupBy(x => x.ModelId!.Value)
            .ToDictionary(x => x.Key, x => x.Count());

        return models
            .Select(x =>
            {
                itemGroups.TryGetValue(x.Id, out var items);
                items ??= [];
                var assessmentCount = assessmentCountByModelId.GetValueOrDefault(x.Id);
                var canEditStructure = assessmentCount == 0;

                return new CompetencyModelAdminListItemDto
                {
                    ModelId = x.Id,
                    Name = x.Name,
                    ScaleType = x.ScaleType,
                    MinScore = x.MinScore,
                    MaxScore = x.MaxScore,
                    ItemCount = items.Count,
                    RequiredItemCount = items.Count(i => i.IsRequired),
                    TotalWeight = items.Count == 0 ? null : items.Sum(i => i.Weight ?? 0m),
                    AssessmentCount = assessmentCount,
                    CanEditStructure = canEditStructure,
                    CanDelete = canEditStructure && items.Count == 0
                };
            })
            .OrderBy(x => x.Name)
            .ToList();
    }

    private static List<CompetencyCatalogAdminListItemDto> MapCompetencies(
        List<Competency> competencies,
        List<CompetencyModelItem> modelItems)
    {
        var modelItemCountByCompetencyId = modelItems
            .GroupBy(x => x.CompetencyId)
            .ToDictionary(x => x.Key, x => x.Count());

        return competencies
            .Select(x => new CompetencyCatalogAdminListItemDto
            {
                CompetencyId = x.Id,
                Code = x.Code,
                Name = x.Name,
                Description = x.Description,
                IsActive = x.IsActive,
                ModelItemCount = modelItemCountByCompetencyId.GetValueOrDefault(x.Id),
                AssessmentItemCount = 0,
                CanDelete = false
            })
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Code)
            .ThenBy(x => x.Name)
            .ToList();
    }

    private static CompetencyModelItemAdminListItemDto MapItem(
        CompetencyModelItem item,
        Dictionary<Guid, Competency> competencyById)
    {
        competencyById.TryGetValue(item.CompetencyId, out var competency);

        return new CompetencyModelItemAdminListItemDto
        {
            ModelItemId = item.Id,
            ModelId = item.ModelId,
            CompetencyId = item.CompetencyId,
            CompetencyCode = competency?.Code ?? string.Empty,
            CompetencyName = competency?.Name ?? string.Empty,
            CompetencyIsActive = competency?.IsActive ?? false,
            Weight = item.Weight,
            IsRequired = item.IsRequired
        };
    }

    private static string NormalizeRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException($"{fieldName}Required");
        }

        return value.Trim();
    }
}
