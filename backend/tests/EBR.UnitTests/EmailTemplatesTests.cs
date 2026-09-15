using EBR.Application.Email;

namespace EBR.UnitTests;

/// <summary>
/// Plantillas HTML de correo: cada una debe traer su texto plano de respaldo, referenciar el logo por
/// el mismo Content-Id que <c>SmtpEmailSender</c> sabe resolver, y nunca dejar pasar sin escapar texto
/// que vino de un usuario (nombre, motivo de rechazo) hacia el HTML.
/// </summary>
public sealed class EmailTemplatesTests
{
    [Fact]
    public void PasswordRecoveryIncludesTheCodeInBothVersions()
    {
        var content = EmailTemplates.PasswordRecovery("4821");

        Assert.Contains("4821", content.PlainText);
        Assert.Contains("4821", content.Html);
        Assert.Contains($"cid:{EmailTemplates.LogoContentId}", content.Html);
    }

    [Fact]
    public void RegistrationApprovedWithoutALoginUrlOmitsTheButton()
    {
        var content = EmailTemplates.RegistrationApproved("Ana Pérez", loginUrl: null);

        Assert.Contains("Ana Pérez", content.PlainText);
        Assert.DoesNotContain("<a href", content.Html);
    }

    [Fact]
    public void RegistrationApprovedWithALoginUrlAddsAnEncodedButtonLink()
    {
        var content = EmailTemplates.RegistrationApproved("Ana Pérez", "http://localhost:5183");

        Assert.Contains("http://localhost:5183", content.Html);
        Assert.Contains("<a href", content.Html);
    }

    [Fact]
    public void RegistrationRejectedEscapesAUserSuppliedReason()
    {
        var content = EmailTemplates.RegistrationRejected("Usuario <Malicioso>", "Motivo con <script>alert(1)</script>");

        Assert.DoesNotContain("<script>", content.Html);
        Assert.Contains("&lt;script&gt;", content.Html);
        Assert.DoesNotContain("<Malicioso>", content.Html);
        // El texto plano no necesita (ni debe) escaparse: no es HTML.
        Assert.Contains("<script>alert(1)</script>", content.PlainText);
    }

    [Fact]
    public void RegistrationRejectedWithoutAReasonOmitsTheReasonBlock()
    {
        var content = EmailTemplates.RegistrationRejected("Ana Pérez", reason: null);

        Assert.DoesNotContain("Motivo", content.Html);
    }

    [Fact]
    public void CaseNotificationReusesTitleAndMessageAsSubjectAndPlainText()
    {
        var content = EmailTemplates.CaseNotification("Nuevo expediente asignado", "Se le asignó el expediente 42.");

        Assert.Equal("Nuevo expediente asignado", content.Subject);
        Assert.Equal("Se le asignó el expediente 42.", content.PlainText);
        // El HTML escapa "ó" a &#243; (correcto: se renderiza igual, pero ya no es un match literal).
        Assert.Contains("Se le asign", content.Html);
        Assert.Contains("el expediente 42.", content.Html);
        Assert.Contains($"cid:{EmailTemplates.LogoContentId}", content.Html);
    }
}
