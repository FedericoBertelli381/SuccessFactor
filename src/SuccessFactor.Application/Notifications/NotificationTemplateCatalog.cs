using System.Collections.Generic;
using System.Linq;
using Volo.Abp.DependencyInjection;

namespace SuccessFactor.Notifications;

public class NotificationTemplateCatalog : ITransientDependency
{
    private static readonly List<NotificationTemplate> Templates =
    [
        new NotificationTemplate
        {
            Code = "CycleOpened",
            Name = "Apertura ciclo",
            Subject = "Apertura ciclo performance: {{CycleName}}",
            Body = "Ciao {{EmployeeName}},\n\nil ciclo performance {{CycleName}} e aperto. Accedi alla piattaforma per completare le attivita assegnate.\n\nGrazie."
        },
        new NotificationTemplate
        {
            Code = "MissingAssessmentReminder",
            Name = "Reminder assessment mancanti",
            Subject = "Reminder assessment: {{CycleName}}",
            Body = "Ciao {{EmployeeName}},\n\nrisultano ancora assessment da completare per il ciclo {{CycleName}}. Ti chiediamo di aggiornare la piattaforma appena possibile.\n\nGrazie."
        },
        new NotificationTemplate
        {
            Code = "AssessmentSubmitted",
            Name = "Submit assessment completato",
            Subject = "Assessment inviato: {{CycleName}}",
            Body = "Ciao {{EmployeeName}},\n\nl'assessment {{AssessmentType}} per il ciclo {{CycleName}} e stato inviato correttamente.\n\nGrazie."
        },
        new NotificationTemplate
        {
            Code = "ManagerReminder",
            Name = "Reminder manager",
            Subject = "Reminder team performance: {{CycleName}}",
            Body = "Ciao {{ManagerName}},\n\nci sono attivita performance ancora aperte per il tuo team nel ciclo {{CycleName}}. Accedi alla sezione Team per verificarle.\n\nGrazie."
        }
    ];

    public List<NotificationTemplate> GetAll()
        => Templates.Select(Clone).ToList();

    public NotificationTemplate GetByCodeOrDefault(string? code)
        => Templates.FirstOrDefault(x => x.Code == code) ?? Templates[0];

    private static NotificationTemplate Clone(NotificationTemplate source)
        => new()
        {
            Code = source.Code,
            Name = source.Name,
            Subject = source.Subject,
            Body = source.Body
        };
}
