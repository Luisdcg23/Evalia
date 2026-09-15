using System.Net;

namespace EBR.Application.Email;

/// <summary>
/// Plantillas HTML de los correos del sistema, con la identidad visual de Evalia (el mismo degradado
/// magenta→cian y la tipografía de la interfaz). Vive en <c>EBR.Application</c> porque es solo
/// composición de texto/markup —sin E/S ni dependencia de proveedor—; quien sabe convertir el
/// <see cref="LogoContentId"/> en una imagen adjunta real es la implementación de
/// <see cref="IEmailSender"/> en <c>EBR.Infrastructure</c>.
/// </summary>
public static class EmailTemplates
{
    /// <summary>
    /// Content-Id lógico del logo dentro del HTML (<c>cid:{LogoContentId}</c>). El remitente SMTP
    /// adjunta la imagen real bajo este mismo identificador solo cuando el cuerpo lo referencia.
    /// </summary>
    public const string LogoContentId = "evalia-logo@evalia.local";

    private const string BrandDark = "#0F172A";
    private const string BrandCyan = "#3BF6E5";
    private const string BrandMagenta = "#E53BF6";
    private const string TextBody = "#334155";
    private const string TextMuted = "#64748B";
    private const string TextFaint = "#94A3B8";
    private const string BorderColor = "#E2E8F0";
    private const string FontStack = "'Segoe UI',Helvetica,Arial,sans-serif";

    public sealed record EmailContent(string Subject, string PlainText, string Html);

    /// <summary>Recuperación de contraseña (RF-01): el código vence en 10 minutos.</summary>
    public static EmailContent PasswordRecovery(string recoveryCode)
    {
        const string subject = "Recuperación de contraseña — Evalia";
        var plainText =
            $"Tu código de recuperación es {recoveryCode}. Vence en 10 minutos. " +
            "Si no solicitaste este cambio, ignora este mensaje.";

        var body = $"""
            <p style="{P}">Recibimos una solicitud para restablecer tu contraseña. Usa el siguiente código dentro de los próximos <strong>10 minutos</strong>:</p>
            <div style="margin:0 0 24px 0;padding:20px 24px;background-color:#ECFEFF;border:1px solid #A5F3FC;border-radius:12px;text-align:center;">
              <span style="font-family:'Courier New',Consolas,monospace;font-size:34px;font-weight:800;letter-spacing:8px;color:{BrandDark};">{Encode(recoveryCode)}</span>
            </div>
            <p style="{PMuted}">Si no solicitaste este cambio, ignora este mensaje: tu contraseña actual sigue siendo válida.</p>
            """;

        return new EmailContent(subject, plainText, Layout("Recupera tu contraseña", body));
    }

    /// <summary>Registro aprobado (RF-02): el usuario ya puede iniciar sesión.</summary>
    public static EmailContent RegistrationApproved(string fullName, string? loginUrl)
    {
        const string subject = "Registro aprobado — Evalia";
        var plainText =
            $"Hola {fullName}, tu registro en Evalia fue aprobado. Ya puedes iniciar sesión con tu correo y contraseña." +
            (string.IsNullOrWhiteSpace(loginUrl) ? "" : $" {loginUrl}");

        var button = string.IsNullOrWhiteSpace(loginUrl) ? "" : Button("Iniciar sesión", loginUrl);
        var body = $"""
            <p style="{P}">Hola {Encode(fullName)},</p>
            <p style="{P}">Tu registro en Evalia fue <strong style="color:#0E7490;">aprobado</strong>. Ya puedes iniciar sesión con tu correo y la contraseña que elegiste al registrarte.</p>
            {button}
            """;

        return new EmailContent(subject, plainText, Layout("¡Registro aprobado!", body));
    }

    /// <summary>Registro rechazado (RF-02): incluye el motivo cuando el administrador lo escribió.</summary>
    public static EmailContent RegistrationRejected(string fullName, string? reason)
    {
        const string subject = "Registro no aprobado — Evalia";
        var plainText = string.IsNullOrWhiteSpace(reason)
            ? $"Hola {fullName}, tu registro en Evalia no fue aprobado."
            : $"Hola {fullName}, tu registro en Evalia no fue aprobado. Motivo: {reason}";

        var reasonBlock = string.IsNullOrWhiteSpace(reason) ? "" : $"""
            <div style="margin:0 0 20px 0;padding:16px 20px;background-color:#FDF2FA;border-left:4px solid {BrandMagenta};border-radius:8px;">
              <p style="margin:0;font-size:13px;font-weight:700;color:{BrandDark};text-transform:uppercase;letter-spacing:0.04em;">Motivo</p>
              <p style="margin:6px 0 0 0;font-size:14px;color:{TextBody};">{Encode(reason!)}</p>
            </div>
            """;
        var body = $"""
            <p style="{P}">Hola {Encode(fullName)},</p>
            <p style="{P}">Revisamos tu solicitud de registro en Evalia y, por ahora, no fue aprobada.</p>
            {reasonBlock}
            <p style="{PMuted}">Si crees que se trata de un error, puedes volver a registrarte con la documentación corregida.</p>
            """;

        return new EmailContent(subject, plainText, Layout("Tu registro no fue aprobado", body));
    }

    /// <summary>
    /// Notificación de expediente (asignación, programación, revisión de informe, cierre). Reutiliza el
    /// mismo título y mensaje que ya se guardan en <c>Notification</c>, dentro de la misma plantilla
    /// visual que el resto de los correos.
    /// </summary>
    public static EmailContent CaseNotification(string title, string message)
    {
        var body = $"""<p style="{P}">{Encode(message)}</p>""";
        return new EmailContent(title, message, Layout(title, body));
    }

    private const string P = "margin:0 0 16px 0;font-size:15px;line-height:1.6;color:" + TextBody + ";";
    private const string PMuted = "margin:0;font-size:13px;line-height:1.5;color:" + TextMuted + ";";

    private static string Button(string label, string url) => $"""
        <table role="presentation" cellpadding="0" cellspacing="0" style="margin:8px 0 4px 0;">
          <tr>
            <td style="border-radius:10px;background-color:{BrandCyan};background-image:linear-gradient(135deg,{BrandMagenta},{BrandCyan});">
              <a href="{Encode(url)}" style="display:inline-block;padding:13px 30px;font-family:{FontStack};font-size:14px;font-weight:700;color:{BrandDark};text-decoration:none;">{Encode(label)}</a>
            </td>
          </tr>
        </table>
        """;

    private static string Layout(string heading, string bodyHtml) => $"""
        <!doctype html>
        <html lang="es">
          <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Evalia</title>
          </head>
          <body style="margin:0;padding:0;background-color:#F1F5F9;font-family:{FontStack};">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:#F1F5F9;padding:32px 16px;">
              <tr>
                <td align="center">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:520px;background-color:#FFFFFF;border-radius:16px;overflow:hidden;box-shadow:0 1px 3px rgba(15,23,42,0.08);">
                    <tr>
                      <td style="background-color:{BrandDark};padding:26px 32px;text-align:center;">
                        <img src="cid:{LogoContentId}" alt="Evalia" height="36" style="display:block;margin:0 auto;height:36px;width:auto;border:0;">
                      </td>
                    </tr>
                    <tr>
                      <td style="height:4px;line-height:4px;font-size:0;background-color:{BrandMagenta};background-image:linear-gradient(90deg,{BrandMagenta},{BrandCyan});">&nbsp;</td>
                    </tr>
                    <tr>
                      <td style="padding:36px 32px 8px 32px;">
                        <h1 style="margin:0 0 18px 0;font-size:21px;font-weight:800;color:{BrandDark};font-family:{FontStack};">{Encode(heading)}</h1>
                        {bodyHtml}
                      </td>
                    </tr>
                    <tr>
                      <td style="padding:12px 32px 28px 32px;">
                        <div style="border-top:1px solid {BorderColor};padding-top:18px;">
                          <p style="margin:0;font-size:12px;color:{TextFaint};font-family:{FontStack};">
                            Este es un mensaje automático de <strong style="color:{TextMuted};">Evalia</strong> — Evaluación Basada en Riesgo. No respondas a este correo.
                          </p>
                        </div>
                      </td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>
          </body>
        </html>
        """;

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
