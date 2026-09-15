namespace EBR.Application.Email;

/// <summary>
/// Envío de correo saliente. Se define con la semántica mínima —destinatario, asunto, texto plano y un
/// cuerpo HTML opcional— para que la implementación real (SMTP, un proveedor gestionado o un sustituto
/// local) pueda cambiarse sin tocar los puntos de la aplicación que disparan un correo (recuperación de
/// contraseña, decisión de registro, notificaciones del flujo de expedientes).
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Envía un correo. <paramref name="plainTextBody"/> siempre viaja como respaldo para clientes que
    /// no rendericen HTML (y es lo único que ve <c>NullEmailSender</c> en el log); si
    /// <paramref name="htmlBody"/> no es <c>null</c>, el correo se envía como <c>multipart/alternative</c>
    /// con ambas versiones. Quien llama decide destinatario, asunto y contenido; el remitente y las
    /// credenciales del proveedor son responsabilidad exclusiva de la implementación.
    /// </summary>
    Task SendAsync(string toEmail, string subject, string plainTextBody, string? htmlBody, CancellationToken cancellationToken);
}
