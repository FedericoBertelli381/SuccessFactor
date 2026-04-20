using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SuccessFactor.Notifications;
using SuccessFactor.Security;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Users;

namespace SuccessFactor.Admin;

[Authorize]
public class NotificationAdminAppService : ApplicationService, INotificationAdminAppService
{
    private readonly ICurrentUser _currentUser;
    private readonly SmtpNotificationEmailSender _emailSender;
    private readonly NotificationTemplateCatalog _templateCatalog;
    private readonly NotificationTemplateRenderer _templateRenderer;

    public NotificationAdminAppService(
        ICurrentUser currentUser,
        SmtpNotificationEmailSender emailSender,
        NotificationTemplateCatalog templateCatalog,
        NotificationTemplateRenderer templateRenderer)
    {
        _currentUser = currentUser;
        _emailSender = emailSender;
        _templateCatalog = templateCatalog;
        _templateRenderer = templateRenderer;
    }

    public Task<NotificationReadinessDto> GetReadinessAsync()
    {
        EnsureAdmin();

        var options = _emailSender.GetOptions();
        var missingSettings = ResolveMissingSettings(options);
        var templates = new List<NotificationTemplateDto>();

        foreach (var template in _templateCatalog.GetAll())
        {
            var rendered = _templateRenderer.Render(template, GetSampleTokens());
            templates.Add(new NotificationTemplateDto
            {
                Code = template.Code,
                Name = template.Name,
                Subject = rendered.Subject,
                BodyPreview = rendered.Body
            });
        }

        return Task.FromResult(new NotificationReadinessDto
        {
            EmailEnabled = options.Enabled,
            Provider = string.IsNullOrWhiteSpace(options.Provider) ? "Smtp" : options.Provider,
            Host = options.Smtp.Host,
            Port = options.Smtp.Port,
            EnableSsl = options.Smtp.EnableSsl,
            FromAddress = options.FromAddress,
            FromName = options.FromName,
            HasUserName = !string.IsNullOrWhiteSpace(options.Smtp.UserName),
            HasPassword = !string.IsNullOrWhiteSpace(options.Smtp.Password),
            IsReady = options.Enabled && missingSettings.Count == 0,
            MissingSettings = missingSettings,
            Templates = templates
        });
    }

    public async Task SendTestEmailAsync(SendTestNotificationInput input)
    {
        EnsureAdmin();

        if (input is null || string.IsNullOrWhiteSpace(input.To))
        {
            throw new BusinessException("TestEmailRecipientRequired");
        }

        var template = _templateCatalog.GetByCodeOrDefault(input.TemplateCode);
        var rendered = _templateRenderer.Render(template, GetSampleTokens());

        await _emailSender.SendAsync(input.To.Trim(), rendered.Subject, rendered.Body);
    }

    private static List<string> ResolveMissingSettings(SuccessFactorEmailOptions options)
    {
        var missing = new List<string>();

        if (!options.Enabled)
        {
            missing.Add("SuccessFactor:Notifications:Email:Enabled");
        }

        if (string.IsNullOrWhiteSpace(options.FromAddress))
        {
            missing.Add("SuccessFactor:Notifications:Email:FromAddress");
        }

        if (string.IsNullOrWhiteSpace(options.Smtp.Host))
        {
            missing.Add("SuccessFactor:Notifications:Email:Smtp:Host");
        }

        if (options.Smtp.Port <= 0)
        {
            missing.Add("SuccessFactor:Notifications:Email:Smtp:Port");
        }

        return missing;
    }

    private static IReadOnlyDictionary<string, string> GetSampleTokens()
        => new Dictionary<string, string>
        {
            ["CycleName"] = "Performance 2026",
            ["EmployeeName"] = "Mario Rossi",
            ["ManagerName"] = "Laura Bianchi",
            ["AssessmentType"] = "Manager"
        };

    private void EnsureAdmin()
    {
        var roles = _currentUser.Roles ?? Array.Empty<string>();

        if (!SuccessFactorRoles.IsAdmin(roles))
        {
            throw new BusinessException("CurrentUserIsNotAdmin");
        }
    }
}
