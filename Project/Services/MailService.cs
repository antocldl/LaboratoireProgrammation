using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace LaboratoireProgrammation.Project.Helpers;

public record MailParameters(
    string SenderEmail,
    string Password,
    IReadOnlyList<string> Recipients,
    string SmtpHost,
    int SmtpPort,
    bool EnableSsl,
    string Subject,
    FlowDocument DocumentBody
);

public static class MailValidator {
    public static bool TryValidate(MailParameters parameters, out string errorMessage) {
        if (string.IsNullOrWhiteSpace(parameters.SenderEmail)) {
            errorMessage = "L'adresse d'expédition est requise.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(parameters.Password)) {
            errorMessage = "Le mot de passe d'authentification est requis.";
            return false;
        }

        if (parameters.Recipients == null || parameters.Recipients.Count == 0) {
            errorMessage = "Au moins un destinataire valide est requis.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(parameters.SmtpHost)) {
            errorMessage = "L'hôte SMTP est requis.";
            return false;
        }

        if (parameters.SmtpPort <= 0 || parameters.SmtpPort > 65535) {
            errorMessage = "Le port SMTP spécifié est invalide.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}

public static class MailService {
    public static async Task SendMailAsync(MailParameters parameters) {
        using var client = new SmtpClient(parameters.SmtpHost, parameters.SmtpPort) {
            EnableSsl = parameters.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(parameters.SenderEmail, parameters.Password),
            Timeout = 15000
        };

        var range = new TextRange(parameters.DocumentBody.ContentStart, parameters.DocumentBody.ContentEnd);
        string plainText = range.Text;

        using var message = new MailMessage {
            From = new MailAddress(parameters.SenderEmail),
            Subject = string.IsNullOrWhiteSpace(parameters.Subject) ? "(Sans objet)" : parameters.Subject,
            Body = plainText,
            IsBodyHtml = false
        };

        foreach (var recipient in parameters.Recipients) {
            message.To.Add(recipient);
        }

        await client.SendMailAsync(message);
    }
}