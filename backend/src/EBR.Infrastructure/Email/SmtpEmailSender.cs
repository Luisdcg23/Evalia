using System.Reflection;
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
    private const string LogoResourceName = "EBR.Infrastructure.Email.Assets.evalia-logo.png";

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

    public async Task SendAsync(string toEmail, string subject, string plainTextBody, string? htmlBody, CancellationToken cancellationToken)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_fromName, _fromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = BuildBody(plainTextBody, htmlBody);

        using var client = new SmtpClient();
        await client.ConnectAsync(_host, _port, SecureSocketOptions.StartTls, cancellationToken);
        await client.AuthenticateAsync(_user, _password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    /// <summary>
    /// Sin HTML, el mensaje es texto plano puro. Con HTML, se arma como <c>multipart/alternative</c> —el
    /// cliente de correo elige la mejor versión que soporte— y, si la plantilla referencia el logo por
    /// <see cref="EmailTemplates.LogoContentId"/>, se adjunta embebido para que se vea sin depender de
    /// que el destinatario cargue imágenes externas.
    /// </summary>
    private static MimeEntity BuildBody(string plainTextBody, string? htmlBody)
    {
        if (htmlBody is null) return new TextPart("plain") { Text = plainTextBody };

        var builder = new BodyBuilder { TextBody = plainTextBody, HtmlBody = htmlBody };
        if (htmlBody.Contains($"cid:{EmailTemplates.LogoContentId}", StringComparison.Ordinal))
        {
            using var logoStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(LogoResourceName)
                ?? throw new InvalidOperationException($"No se encontró el recurso embebido '{LogoResourceName}'.");
            var logo = builder.LinkedResources.Add("evalia-logo.png", logoStream);
            logo.ContentId = EmailTemplates.LogoContentId;
        }

        return builder.ToMessageBody();
    }
}
