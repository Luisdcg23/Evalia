using System.Collections.Concurrent;
using EBR.Application.Email;

namespace EBR.IntegrationTests;

/// <summary>
/// Implementación de <see cref="IEmailSender"/> para las pruebas de integración: en vez de enviar
/// nada, guarda cada correo en memoria para que la suite pueda verificar destinatario, asunto y ambas
/// versiones del cuerpo sin depender de un proveedor SMTP real.
/// </summary>
public sealed class RecordingEmailSender : IEmailSender
{
    public sealed record SentEmail(string ToEmail, string Subject, string PlainTextBody, string? HtmlBody);

    private readonly ConcurrentQueue<SentEmail> _sent = new();

    public IReadOnlyCollection<SentEmail> SentEmails => _sent.ToArray();

    public Task SendAsync(string toEmail, string subject, string plainTextBody, string? htmlBody, CancellationToken cancellationToken)
    {
        _sent.Enqueue(new SentEmail(toEmail, subject, plainTextBody, htmlBody));
        return Task.CompletedTask;
    }
}
