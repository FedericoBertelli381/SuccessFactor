using System;
using Volo.Abp;

namespace SuccessFactor.Blazor.Components.Shared;

public static class UiErrorMessage
{
    public static string From(Exception exception)
    {
        if (exception is BusinessException businessException)
        {
            return FromCode(businessException.Code)
                ?? FromRawMessage(businessException.Message)
                ?? businessException.Code
                ?? "Errore applicativo.";
        }

        return FromRawMessage(exception.Message)
            ?? exception.GetType().Name;
    }

    public static string? FromCode(string? code)
    {
        return code switch
        {
            "EmployeeNotLinkedToUser" or "SuccessFactor:EmployeeNotLinkedToUser" =>
                "Utente non collegato a Employee. Collega l'utente ABP a un record Employee prima di accedere all'area My.",
            "UserNotAuthenticated" or "SuccessFactor:UserNotAuthenticated" =>
                "Utente non autenticato.",
            "TenantMissing" or "SuccessFactor:TenantMissing" =>
                "Tenant non valorizzato.",
            "CycleNotFound" or "SuccessFactor:CycleNotFound" =>
                "Nessun ciclo attivo trovato.",
            "ParticipantNotFound" or "SuccessFactor:ParticipantNotFound" =>
                "Employee non presente tra i partecipanti del ciclo.",
            "ParticipantHasNoPhase" or "SuccessFactor:ParticipantHasNoPhase" =>
                "Il partecipante non ha una fase corrente configurata.",
            _ => null
        };
    }

    private static string? FromRawMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)
            || message.StartsWith("Exception of type ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return message;
    }
}
