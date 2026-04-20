namespace SuccessFactor.Notifications;

public class SuccessFactorEmailOptions
{
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "Smtp";
    public string? FromAddress { get; set; }
    public string? FromName { get; set; }
    public SmtpOptions Smtp { get; set; } = new();
}

public class SmtpOptions
{
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public bool EnableSsl { get; set; } = true;
}
