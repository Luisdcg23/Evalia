using EBR.Application.Email;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace EBR.Infrastructure.Email;

/// <summary>
/// Implementación de <see cref="IEmailSender"/> sobre SMTP (MailKit). Se activa cuando la configuración
/// declara un host en <c>Email:Host</c>; en su ausencia la aplicación usa <see cref="NullEmailSender"/>,
/// que solo registra el correo en el log. Pensada para un proveedor SMTP estándar (p. ej. Gmail con
/// contraseña de aplicación) con STARTTLS en el puerto 587.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _user;
    private readonly string _password;
    private readonly string _fromAddress;
    private readonly string _fromName;

    public SmtpEmailSender(IOptions<EmailOptions> options)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Host))
            throw new InvalidOperationException("Email:Host es obligatorio para usar el envío SMTP.");
        if (string.IsNullOrWhiteSpace(settings.User) || string.IsNullOrWhiteSpace(settings.Password))
            throw new InvalidOperationException("Email:User y Email:Password son obligatorios para usar el envío SMTP.");

        _host = settings.Host;
        _port = settings.Port;
        _user = settings.User;
        _password = settings.Password;
        _fromAddress = string.IsNullOrWhiteSpace(settings.FromAddress) ? settings.User : settings.FromAddress;
        _fromName = settings.FromName;
    }

    public async Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_fromName, _fromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        await client.ConnectAsync(_host, _port, SecureSocketOptions.StartTls, cancellationToken);
        await client.AuthenticateAsync(_user, _password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
