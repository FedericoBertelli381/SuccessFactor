using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SuccessFactor.Auditing;
using SuccessFactor.Security;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace SuccessFactor.Admin;

[Authorize]
public class BusinessAuditAdminAppService : ApplicationService, IBusinessAuditAdminAppService
{
    private readonly ICurrentUser _currentUser;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IRepository<BusinessAuditEvent, Guid> _eventRepository;
    private readonly IDataFilter<IMultiTenant> _multiTenantFilter;

    public BusinessAuditAdminAppService(
        ICurrentUser currentUser,
        IAsyncQueryableExecuter asyncExecuter,
        IRepository<BusinessAuditEvent, Guid> eventRepository,
        IDataFilter<IMultiTenant> multiTenantFilter)
    {
        _currentUser = currentUser;
        _asyncExecuter = asyncExecuter;
        _eventRepository = eventRepository;
        _multiTenantFilter = multiTenantFilter;
    }

    public async Task<BusinessAuditEventListDto> GetAsync(GetBusinessAuditEventsInput input)
    {
        EnsureAdmin();
        input ??= new GetBusinessAuditEventsInput();
        input.MaxResultCount = Math.Clamp(input.MaxResultCount, 1, 200);
        input.SkipCount = Math.Max(0, input.SkipCount);

        if (CurrentTenant.Id is null)
        {
            using (_multiTenantFilter.Disable())
            {
                return await QueryAsync(input);
            }
        }

        return await QueryAsync(input);
    }

    private async Task<BusinessAuditEventListDto> QueryAsync(GetBusinessAuditEventsInput input)
    {
        var query = await _eventRepository.GetQueryableAsync();

        if (input.From.HasValue)
        {
            query = query.Where(x => x.EventTime >= input.From.Value);
        }

        if (input.To.HasValue)
        {
            query = query.Where(x => x.EventTime <= input.To.Value);
        }

        if (!string.IsNullOrWhiteSpace(input.User))
        {
            var user = input.User.Trim();
            var userId = Guid.TryParse(user, out var parsedUserId) ? parsedUserId : (Guid?)null;
            query = query.Where(x =>
                (x.UserName != null && x.UserName.Contains(user)) ||
                (userId.HasValue && x.UserId == userId.Value));
        }

        if (!string.IsNullOrWhiteSpace(input.Action))
        {
            var action = input.Action.Trim();
            query = query.Where(x => x.Action.Contains(action));
        }

        if (!string.IsNullOrWhiteSpace(input.EntityType))
        {
            var entityType = input.EntityType.Trim();
            query = query.Where(x => x.EntityType.Contains(entityType));
        }

        if (!string.IsNullOrWhiteSpace(input.EntityId))
        {
            var entityId = input.EntityId.Trim();
            query = query.Where(x => x.EntityId != null && x.EntityId.Contains(entityId));
        }

        if (input.TenantId.HasValue)
        {
            query = query.Where(x => x.TenantId == input.TenantId.Value);
        }

        var totalCount = await _asyncExecuter.CountAsync(query);
        var items = await _asyncExecuter.ToListAsync(
            query
                .OrderByDescending(x => x.EventTime)
                .ThenByDescending(x => x.Id)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount));

        return new BusinessAuditEventListDto
        {
            TotalCount = totalCount,
            Items = items.Select(x => new BusinessAuditEventDto
            {
                Id = x.Id,
                TenantId = x.TenantId,
                UserId = x.UserId,
                UserName = x.UserName,
                Action = x.Action,
                EntityType = x.EntityType,
                EntityId = x.EntityId,
                EventTime = x.EventTime,
                Payload = x.Payload
            }).ToList()
        };
    }

    private void EnsureAdmin()
    {
        var roles = _currentUser.Roles ?? Array.Empty<string>();

        if (!SuccessFactorRoles.IsAdmin(roles))
        {
            throw new BusinessException("CurrentUserIsNotAdmin");
        }
    }
}
