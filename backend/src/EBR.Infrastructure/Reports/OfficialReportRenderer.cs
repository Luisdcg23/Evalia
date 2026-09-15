using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using EBR.Application.Reports;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EBR.Infrastructure.Reports;

/// <summary>
/// Compone el PDF oficial del informe con QuestPDF (licencia Community). No calcula nada: recibe los
/// datos ya persistidos e inmutables y los maqueta. La salida es determinista —los metadatos de fecha
/// del documento se fijan a la fecha de emisión del informe, no a la de generación— para que dos
/// emisiones del mismo informe produzcan el mismo archivo.
/// </summary>
public sealed class OfficialReportRenderer : IOfficialReportRenderer
{
    /// <summary>
    /// Nombre de familia con el que "Alex Brush" queda registrada ante QuestPDF/SkiaSharp; es el mismo
    /// nombre declarado dentro del propio archivo TTF (SIL OFL 1.1, ver Reports/Assets/AlexBrush-OFL.txt).
    /// </summary>
    private const string SignatureFontFamily = "Alex Brush";

    static OfficialReportRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.EnableDebugging = false;

        using var fontStream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("EBR.Infrastructure.Reports.Assets.AlexBrush-Regular.ttf")
            ?? throw new InvalidOperationException("No se encontró la fuente embebida de la firma visual (AlexBrush-Regular.ttf).");
        FontManager.RegisterFont(fontStream);
    }

    private static readonly IReadOnlyDictionary<string, string> SeverityLabels = new Dictionary<string, string>
    {
        ["CRITICAL"] = "Críticas",
        ["MAJOR"] = "Mayores",
        ["MINOR"] = "Menores"
    };

    public RenderedOfficialReport Render(OfficialReportContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var contentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Canonicalize(content))))
            .ToLowerInvariant();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(style => style.FontSize(10).FontFamily(Fonts.Arial));

                page.Header().Column(header =>
                {
                    header.Item().Text("INFORME OFICIAL DE EVALUACIÓN BPM").Bold().FontSize(15);
                    header.Item().Text(text =>
                    {
                        text.Span("Expediente ").SemiBold();
                        text.Span($"{content.CaseId}  ·  ");
                        text.Span("Evaluación ").SemiBold();
                        text.Span($"{content.EvaluationInstanceId}  ·  ");
                        text.Span("Versión ").SemiBold();
                        text.Span($"{content.Version}");
                    });
                    header.Item().Text($"{content.CompanyName} (RNC {content.CompanyRnc})");
                    header.Item().Text($"Fecha de emisión del informe: {Format(content.ReportIssuedAt)}");
                });

                page.Content().PaddingVertical(12).Column(body =>
                {
                    body.Spacing(14);

                    Section(body, "Resumen ejecutivo", content.ExecutiveSummary);
                    Section(body, "Hallazgos", content.Findings);

                    body.Item().Column(result =>
                    {
                        result.Item().Text("Resultado BPM y de riesgo").Bold().FontSize(12);
                        result.Item().Text($"Cumplimiento BPM: {Number(content.BpmPercentage)}%");
                        result.Item().Text($"Calificación: {content.QualificationCode} — {content.Classification}");
                        result.Item().Text($"Puntaje de riesgo BPM: {Number(content.BpmRiskScore)}");
                        result.Item().Text($"Frecuencia de inspección asignada: {content.FrequencyMonths} meses");
                    });

                    body.Item().Column(counts =>
                    {
                        counts.Item().Text("No conformidades por severidad").Bold().FontSize(12);
                        counts.Item().Text($"Críticas: {content.CriticalCount}  ·  Mayores: {content.MajorCount}  ·  Menores: {content.MinorCount}");
                    });

                    if (content.NonConformities.Count > 0)
                    {
                        body.Item().Column(detail =>
                        {
                            detail.Spacing(6);
                            foreach (var group in OrderedGroups(content.NonConformities))
                            {
                                detail.Item().Text(SeverityLabels.GetValueOrDefault(group.Key, group.Key)).SemiBold();
                                foreach (var item in group)
                                    detail.Item().Text($"  {item.CriterionCode}: {item.Description}");
                            }
                        });
                    }

                    Section(body, "Recomendaciones", content.Recommendations);

                    body.Item().Column(evidence =>
                    {
                        evidence.Item().Text("Referencias de evidencias").Bold().FontSize(12);
                        if (content.Evidences.Count == 0)
                        {
                            evidence.Item().Text("Sin evidencias registradas.");
                        }
                        else
                        {
                            foreach (var item in content.Evidences.OrderBy(value => value.FileName, StringComparer.Ordinal))
                                evidence.Item().Text($"  {item.FileName}  ·  {item.SizeBytes} bytes  ·  SHA-256 {item.Sha256}");
                        }
                    });

                    SignatureBlock(body, content);
                });

                page.Footer().Text(text =>
                {
                    text.Span("Documento generado el ").FontSize(8);
                    text.Span(Format(content.GeneratedAt)).FontSize(8);
                    text.Span("  ·  Hash de contenido ").FontSize(8);
                    text.Span(contentHash).FontSize(8);
                });
            });
        });

        document.WithMetadata(new DocumentMetadata
        {
            Title = $"Informe oficial de evaluación {content.EvaluationInstanceId} v{content.Version}",
            Author = "Sistema EBR/BPM",
            Subject = $"Expediente {content.CaseId}",
            Keywords = contentHash,
            // Se fijan a la emisión del informe, no a la generación del PDF, para que el archivo sea
            // reproducible entre generaciones del mismo informe.
            CreationDate = content.ReportIssuedAt,
            ModifiedDate = content.ReportIssuedAt
        });
        document.WithSettings(new DocumentSettings
        {
            CompressDocument = true,
            ImageCompressionQuality = ImageCompressionQuality.High,
            ImageRasterDpi = 144
        });

        return new RenderedOfficialReport(document.GeneratePdf(), contentHash);
    }

    /// <summary>
    /// Las dos firmas visuales del expediente (RF-19), al estilo de un documento firmado
    /// electrónicamente (p. ej. Adobe Sign): el técnico que emitió la versión y el coordinador que la
    /// aprobó, cada uno con la rúbrica que escribió impresa en cursiva y su nombre registrado debajo a
    /// manera de aclaración. Que sean dos responde al recorrido del informe —quien levanta el acta y
    /// quien la valida—, y la rúbrica no identifica a nadie por sí sola: la aclaración sale del usuario
    /// autenticado. No es la firma criptográfica —esa vive en el metadato, ver <c>IDocumentSigner</c>—,
    /// es la representación visual esperada en un documento oficial.
    /// </summary>
    private static void SignatureBlock(ColumnDescriptor column, OfficialReportContent content)
    {
        column.Item().PaddingTop(24).Column(block =>
        {
            block.Item().Text("Firmas electrónicas").Bold().FontSize(11).FontColor(Colors.Grey.Darken1);
            block.Item().PaddingTop(8).Row(row =>
            {
                Signer(row.RelativeItem(), content.TechnicianSignatureName, content.TechnicianFullName,
                    "Técnico Evaluador", "Emitido", content.ReportIssuedAt);
                row.ConstantItem(28);
                Signer(row.RelativeItem(), content.ApproverSignatureName, content.ApproverFullName,
                    "Coordinador · Evaluación Basada en Riesgo", "Aprobado", content.ApprovedAt);
            });
        });
    }

    private static void Signer(
        IContainer container, string rubric, string fullName, string role, string label, DateTimeOffset signedAt)
    {
        container.Column(signature =>
        {
            signature.Item().PaddingLeft(6).Text(rubric)
                .FontFamily(SignatureFontFamily).FontSize(26).FontColor(Colors.Black);
            signature.Item().PaddingTop(2).LineHorizontal(0.75f).LineColor(Colors.Grey.Darken2);
            signature.Item().PaddingTop(4).Text(fullName).FontSize(9).SemiBold();
            signature.Item().Text(role).FontSize(8).FontColor(Colors.Grey.Darken1);
            signature.Item().Text($"{label} {Format(signedAt)}").FontSize(7).FontColor(Colors.Grey.Darken1);
        });
    }

    private static void Section(ColumnDescriptor column, string title, string text)
    {
        column.Item().Column(section =>
        {
            section.Item().Text(title).Bold().FontSize(12);
            section.Item().Text(string.IsNullOrWhiteSpace(text) ? "—" : text);
        });
    }

    private static IEnumerable<IGrouping<string, OfficialReportNonConformity>> OrderedGroups(
        IReadOnlyList<OfficialReportNonConformity> items)
    {
        var rank = new Dictionary<string, int> { ["CRITICAL"] = 0, ["MAJOR"] = 1, ["MINOR"] = 2 };
        return items
            .OrderBy(item => rank.GetValueOrDefault(item.Severity, 9))
            .ThenBy(item => item.CriterionCode, StringComparer.Ordinal)
            .GroupBy(item => item.Severity);
    }

    private static string Canonicalize(OfficialReportContent content)
    {
        var builder = new StringBuilder();
        builder.Append("v2\n");
        builder.Append(content.EvaluationInstanceId).Append('\n');
        builder.Append(content.CaseId).Append('\n');
        builder.Append(content.CompanyName).Append('\n');
        builder.Append(content.CompanyRnc).Append('\n');
        builder.Append(content.Version).Append('\n');
        builder.Append(content.ExecutiveSummary).Append('\n');
        builder.Append(content.Findings).Append('\n');
        builder.Append(content.Recommendations).Append('\n');
        builder.Append(Number(content.BpmPercentage)).Append('\n');
        builder.Append(content.QualificationCode).Append('\n');
        builder.Append(content.Classification).Append('\n');
        builder.Append(Number(content.BpmRiskScore)).Append('\n');
        builder.Append(content.FrequencyMonths).Append('\n');
        builder.Append(content.CriticalCount).Append('\n');
        builder.Append(content.MajorCount).Append('\n');
        builder.Append(content.MinorCount).Append('\n');
        builder.Append(content.ReportIssuedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)).Append('\n');
        builder.Append(content.ApproverFullName).Append('\n');
        builder.Append(content.ApproverSignatureName).Append('\n');
        builder.Append(content.ApprovedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)).Append('\n');
        builder.Append(content.TechnicianFullName).Append('\n');
        builder.Append(content.TechnicianSignatureName).Append('\n');
        foreach (var item in content.NonConformities
                     .OrderBy(value => value.Severity, StringComparer.Ordinal)
                     .ThenBy(value => value.CriterionCode, StringComparer.Ordinal)
                     .ThenBy(value => value.Description, StringComparer.Ordinal))
            builder.Append("NC\t").Append(item.Severity).Append('\t').Append(item.CriterionCode)
                .Append('\t').Append(item.Description).Append('\n');
        foreach (var item in content.Evidences
                     .OrderBy(value => value.FileName, StringComparer.Ordinal)
                     .ThenBy(value => value.Sha256, StringComparer.Ordinal))
            builder.Append("EV\t").Append(item.FileName).Append('\t').Append(item.Sha256)
                .Append('\t').Append(item.SizeBytes).Append('\n');
        return builder.ToString();
    }

    private static string Number(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Format(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
}
