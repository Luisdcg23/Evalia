namespace EBR.Application.Email;

/// <summary>
/// Envío de correo saliente. Se define con la semántica mínima —destinatario, asunto y cuerpo— para
/// que la implementación real (SMTP, un proveedor gestionado o un sustituto local) pueda cambiarse sin
/// tocar los puntos de la aplicación que disparan un correo (recuperación de contraseña, notificaciones
/// del flujo de expedientes).
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Envía un correo en texto plano. Quien llama decide destinatario, asunto y cuerpo; el remitente
    /// y las credenciales del proveedor son responsabilidad exclusiva de la implementación.
    /// </summary>
    Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken);
}
