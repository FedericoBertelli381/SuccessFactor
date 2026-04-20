using System.Collections.Generic;

namespace SuccessFactor.Admin;

public class NotificationReadinessDto
{
    public bool EmailEnabled { get; set; }
    public string Provider { get; set; } = "Smtp";
    public string? Host { get; set; }
    public int Port { get; set; }
    public bool EnableSsl { get; set; }
    public string? FromAddress { get; set; }
    public string? FromName { get; set; }
    public bool HasUserName { get; set; }
    public bool HasPassword { get; set; }
    public bool IsReady { get; set; }
    public List<string> MissingSettings { get; set; } = [];
    public List<NotificationTemplateDto> Templates { get; set; } = [];
}
