namespace EBR.Infrastructure.Email;

/// <summary>
/// Configuración del envío de correo (sección <c>Email</c>, variables <c>Email__*</c> del archivo
/// <c>.env</c>). Con <see cref="Host"/> definido se usa SMTP real; sin él, <see cref="NullEmailSender"/>.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>Host SMTP. Si está vacío, no se envía correo real (ver <see cref="NullEmailSender"/>).</summary>
    public string? Host { get; set; }

    public int Port { get; set; } = 587;
    public string? User { get; set; }
    public string? Password { get; set; }

    /// <summary>Dirección remitente; si no se define, se usa <see cref="User"/>.</summary>
    public string? FromAddress { get; set; }

    public string FromName { get; set; } = "Evalia";
}
