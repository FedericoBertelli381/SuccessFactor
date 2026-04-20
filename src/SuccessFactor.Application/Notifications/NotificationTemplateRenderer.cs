using System.Collections.Generic;
using Volo.Abp.DependencyInjection;

namespace SuccessFactor.Notifications;

public class NotificationTemplateRenderer : ITransientDependency
{
    public (string Subject, string Body) Render(NotificationTemplate template, IReadOnlyDictionary<string, string> tokens)
    {
        var subject = template.Subject;
        var body = template.Body;

        foreach (var token in tokens)
        {
            var placeholder = "{{" + token.Key + "}}";
            subject = subject.Replace(placeholder, token.Value);
            body = body.Replace(placeholder, token.Value);
        }

        return (subject, body);
    }
}
