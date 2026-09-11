using EBR.Application.Email;
using Microsoft.Extensions.Logging;

namespace EBR.Infrastructure.Email;

/// <summary>
/// Sustituto de <see cref="IEmailSender"/> que no envía nada: solo deja constancia en el log. Es el
/// que se usa cuando no hay <c>Email:Host</c> configurado, para poder ejecutar la API y las pruebas sin
/// depender de un proveedor SMTP real.
/// </summary>
public sealed partial class NullEmailSender(ILogger<NullEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken)
    {
        LogEmailSkipped(logger, toEmail, subject);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Correo no enviado (sin proveedor configurado) para {ToEmail}: {Subject}")]
    private static partial void LogEmailSkipped(ILogger logger, string toEmail, string subject);
}
