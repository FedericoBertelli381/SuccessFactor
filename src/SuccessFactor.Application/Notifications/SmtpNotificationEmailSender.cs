using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace SuccessFactor.Notifications;

public class SmtpNotificationEmailSender : ITransientDependency
{
    private readonly IConfiguration _configuration;

    public SmtpNotificationEmailSender(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public SuccessFactorEmailOptions GetOptions()
    {
        var options = new SuccessFactorEmailOptions();
        _configuration.GetSection("SuccessFactor:Notifications:Email").Bind(options);
        return options;
    }

    public async Task SendAsync(string to, string subject, string body)
    {
        var options = GetOptions();

        if (!options.Enabled)
        {
            throw new BusinessException("EmailNotificationsDisabled");
        }

        ValidateOptions(options);

        using var message = new MailMessage
        {
            From = new MailAddress(options.FromAddress!, options.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        message.To.Add(to);

        using var client = new SmtpClient(options.Smtp.Host!, options.Smtp.Port)
        {
            EnableSsl = options.Smtp.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(options.Smtp.UserName))
        {
            client.Credentials = new NetworkCredential(options.Smtp.UserName, options.Smtp.Password);
        }

        await client.SendMailAsync(message);
    }

    private static void ValidateOptions(SuccessFactorEmailOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.FromAddress))
        {
            throw new BusinessException("EmailFromAddressMissing");
        }

        if (string.IsNullOrWhiteSpace(options.Smtp.Host))
        {
            throw new BusinessException("EmailSmtpHostMissing");
        }

        if (options.Smtp.Port <= 0)
        {
            throw new BusinessException("EmailSmtpPortInvalid");
        }
    }
}
