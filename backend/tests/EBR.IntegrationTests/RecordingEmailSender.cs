using System.Collections.Concurrent;
using EBR.Application.Email;

namespace EBR.IntegrationTests;

/// <summary>
/// Implementación de <see cref="IEmailSender"/> para las pruebas de integración: en vez de enviar
/// nada, guarda cada correo en memoria para que la suite pueda verificar destinatario, asunto y
/// cuerpo sin depender de un proveedor SMTP real.
/// </summary>
public sealed class RecordingEmailSender : IEmailSender
{
    public sealed record SentEmail(string ToEmail, string Subject, string Body);

    private readonly ConcurrentQueue<SentEmail> _sent = new();

    public IReadOnlyCollection<SentEmail> SentEmails => _sent.ToArray();

    public Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken)
    {
        _sent.Enqueue(new SentEmail(toEmail, subject, body));
        return Task.CompletedTask;
    }
}
