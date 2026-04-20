using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Users;

namespace SuccessFactor.Auditing;

public class BusinessAuditLogger : IBusinessAuditLogger, ITransientDependency
{
    private readonly ILogger<BusinessAuditLogger> _logger;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentTenant _currentTenant;
    private readonly IClock _clock;
    private readonly IRepository<BusinessAuditEvent, Guid> _eventRepository;
    private readonly IGuidGenerator _guidGenerator;

    public BusinessAuditLogger(
        ILogger<BusinessAuditLogger> logger,
        ICurrentUser currentUser,
        ICurrentTenant currentTenant,
        IClock clock,
        IRepository<BusinessAuditEvent, Guid> eventRepository,
        IGuidGenerator guidGenerator)
    {
        _logger = logger;
        _currentUser = currentUser;
        _currentTenant = currentTenant;
        _clock = clock;
        _eventRepository = eventRepository;
        _guidGenerator = guidGenerator;
    }

    public Task LogAsync(
        string action,
        string entityType,
        string? entityId = null,
        IReadOnlyDictionary<string, object?>? data = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["Action"] = action,
            ["EntityType"] = entityType,
            ["EntityId"] = entityId,
            ["TenantId"] = _currentTenant.Id,
            ["UserId"] = _currentUser.Id,
            ["UserName"] = _currentUser.UserName,
            ["Time"] = _clock.Now
        };

        if (data is not null)
        {
            foreach (var item in data)
            {
                payload[item.Key] = item.Value;
            }
        }

        var serializedPayload = JsonSerializer.Serialize(payload);

        _logger.LogInformation(
            "BusinessAudit {Action} {EntityType} {EntityId} {TenantId} {UserId} {UserName} {Payload}",
            action,
            entityType,
            entityId,
            _currentTenant.Id,
            _currentUser.Id,
            _currentUser.UserName,
            serializedPayload);

        return _eventRepository.InsertAsync(new BusinessAuditEvent(_guidGenerator.Create())
        {
            TenantId = _currentTenant.Id,
            UserId = _currentUser.Id,
            UserName = _currentUser.UserName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            EventTime = _clock.Now,
            Payload = serializedPayload
        }, autoSave: true);
    }
}
