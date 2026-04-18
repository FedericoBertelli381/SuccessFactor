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
public class AdminCompetencyCatalogAppService : ApplicationService, IAdminCompetencyCatalogAppService
{
    private readonly ICurrentUser _currentUser;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IRepository<Competency, Guid> _competencyRepository;
    private readonly IRepository<CompetencyModelItem, Guid> _modelItemRepository;
    private readonly IRepository<CompetencyAssessmentItem, Guid> _assessmentItemRepository;

    public AdminCompetencyCatalogAppService(
        ICurrentUser currentUser,
        IAsyncQueryableExecuter asyncExecuter,
        IRepository<Competency, Guid> competencyRepository,
        IRepository<CompetencyModelItem, Guid> modelItemRepository,
        IRepository<CompetencyAssessmentItem, Guid> assessmentItemRepository)
    {
        _currentUser = currentUser;
        _asyncExecuter = asyncExecuter;
        _competencyRepository = competencyRepository;
        _modelItemRepository = modelItemRepository;
        _assessmentItemRepository = assessmentItemRepository;
    }

    public async Task<CompetencyCatalogAdminDto> GetAsync()
    {
        EnsureTenantAndAdmin();

        var competencyQuery = await _competencyRepository.GetQueryableAsync();
        var competencies = await _asyncExecuter.ToListAsync(
            competencyQuery
                .OrderByDescending(x => x.IsActive)
                .ThenBy(x => x.Code)
                .ThenBy(x => x.Name));

        var modelItems = await _modelItemRepository.GetListAsync();
        var assessmentItems = await _assessmentItemRepository.GetListAsync();

        return new CompetencyCatalogAdminDto
        {
            Competencies = MapCompetencies(competencies, modelItems, assessmentItems)
        };
    }

    public async Task<CompetencyCatalogAdminListItemDto> SaveAsync(Guid? competencyId, SaveCompetencyCatalogItemInput input)
    {
        EnsureTenantAndAdmin();
        NormalizeAndValidate(input);

        var competencyQuery = await _competencyRepository.GetQueryableAsync();
        var competencies = await _asyncExecuter.ToListAsync(competencyQuery);
        EnsureNoDuplicateCode(competencyId, input.Code, competencies);

        Competency entity;

        if (competencyId.HasValue)
        {
            entity = await _competencyRepository.GetAsync(competencyId.Value);
        }
        else
        {
            entity = new Competency
            {
                TenantId = CurrentTenant.Id
            };
        }

        entity.Code = input.Code;
        entity.Name = input.Name;
        entity.Description = input.Description;
        entity.IsActive = input.IsActive;

        entity = competencyId.HasValue
            ? await _competencyRepository.UpdateAsync(entity, autoSave: true)
            : await _competencyRepository.InsertAsync(entity, autoSave: true);

        var refreshedCompetencies = await _competencyRepository.GetListAsync();
        var modelItems = await _modelItemRepository.GetListAsync();
        var assessmentItems = await _assessmentItemRepository.GetListAsync();

        return MapCompetencies(refreshedCompetencies, modelItems, assessmentItems)
            .Single(x => x.CompetencyId == entity.Id);
    }

    public async Task DeleteAsync(Guid competencyId)
    {
        EnsureTenantAndAdmin();

        if (competencyId == Guid.Empty)
        {
            throw new BusinessException("CompetencyIdRequired");
        }

        if (await _modelItemRepository.AnyAsync(x => x.CompetencyId == competencyId) ||
            await _assessmentItemRepository.AnyAsync(x => x.CompetencyId == competencyId))
        {
            throw new BusinessException("CompetencyIsReferenced");
        }

        await _competencyRepository.DeleteAsync(competencyId);
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

    private static void NormalizeAndValidate(SaveCompetencyCatalogItemInput input)
    {
        input.Code = NormalizeRequired(input.Code, "Code").ToUpperInvariant();
        input.Name = NormalizeRequired(input.Name, "Name");
        input.Description = NormalizeOptional(input.Description);
    }

    private static void EnsureNoDuplicateCode(Guid? competencyId, string code, List<Competency> competencies)
    {
        if (competencies.Any(x =>
            string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase) &&
            (!competencyId.HasValue || x.Id != competencyId.Value)))
        {
            throw new BusinessException("CompetencyCodeAlreadyExists");
        }
    }

    private static List<CompetencyCatalogAdminListItemDto> MapCompetencies(
        List<Competency> competencies,
        List<CompetencyModelItem> modelItems,
        List<CompetencyAssessmentItem> assessmentItems)
    {
        var modelItemCountByCompetencyId = modelItems
            .GroupBy(x => x.CompetencyId)
            .ToDictionary(x => x.Key, x => x.Count());

        var assessmentItemCountByCompetencyId = assessmentItems
            .GroupBy(x => x.CompetencyId)
            .ToDictionary(x => x.Key, x => x.Count());

        return competencies
            .Select(x =>
            {
                var modelItemCount = modelItemCountByCompetencyId.GetValueOrDefault(x.Id);
                var assessmentItemCount = assessmentItemCountByCompetencyId.GetValueOrDefault(x.Id);

                return new CompetencyCatalogAdminListItemDto
                {
                    CompetencyId = x.Id,
                    Code = x.Code,
                    Name = x.Name,
                    Description = x.Description,
                    IsActive = x.IsActive,
                    ModelItemCount = modelItemCount,
                    AssessmentItemCount = assessmentItemCount,
                    CanDelete = modelItemCount == 0 && assessmentItemCount == 0
                };
            })
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Code)
            .ThenBy(x => x.Name)
            .ToList();
    }

    private static string NormalizeRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException($"{fieldName}Required");
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return normalized == "-" ? null : normalized;
    }
}
