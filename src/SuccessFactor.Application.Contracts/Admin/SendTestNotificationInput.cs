namespace SuccessFactor.Admin;

public class SendTestNotificationInput
{
    public string To { get; set; } = string.Empty;
    public string? TemplateCode { get; set; }
}
