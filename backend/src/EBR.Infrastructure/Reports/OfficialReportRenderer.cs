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
///
/// Con <see cref="OfficialReportContent.IsPreview"/> compone la vista previa que el coordinador ve antes
/// de decidir: mismo contenido, pero con marca de agua "NO OFICIAL", el estado real de la versión del
/// informe y sin hash ni firma. El modo no altera el hash de contenido.
/// </summary>
public sealed class OfficialReportRenderer : IOfficialReportRenderer
{
    /// <summary>
    /// Nombre de familia con el que "Alex Brush" queda registrada ante QuestPDF/SkiaSharp; es el mismo
    /// nombre declarado dentro del propio archivo TTF (SIL OFL 1.1, ver Reports/Assets/AlexBrush-OFL.txt).
    /// </summary>
    private const string SignatureFontFamily = "Alex Brush";

    private const string LogoResourceName = "EBR.Infrastructure.Reports.Assets.evalia-logo.png";

    /// <summary>Ancho del logo en el encabezado, en puntos; el alto se deduce de su proporción.</summary>
    private const float HeaderLogoWidth = 92;

    /// <summary>Número de tramos con que se dibuja la franja degradada de la marca.</summary>
    private const int GradientSteps = 40;

    /// <summary>Violeta del degradado del logo con transparencia, para la marca de agua.</summary>
    private static readonly Color WatermarkColor = Color.FromARGB(46, 183, 93, 245);

    /// <summary>
    /// Paleta tomada del logo y de la interfaz de Evalia (degradado magenta → violeta → cian sobre
    /// fondo morado oscuro), adaptada a papel: los oscuros de la app se usan para franjas y encabezados
    /// de tabla y el resto del documento va sobre blanco. Solo colores; no afecta al contenido ni al hash.
    /// </summary>
    private static class Palette
    {
        public const string White = "#FFFFFF";
        public const string Primary = "#1F0B38";
        public const string Accent = "#B75DF5";
        public const string Ink = "#1F1B2E";
        public const string Muted = "#62597A";
        public const string Surface = "#F7F3FD";
        public const string Line = "#E4DBF3";
        public const string OnPrimarySoft = "#CDBFE8";
        public const string Danger = "#B42318";
        public const string WarningText = "#93370D";
        public const string WarningBackground = "#FFF4DB";
    }

    /// <summary>Paradas del degradado del logo, de izquierda (magenta) a derecha (cian), en RGB.</summary>
    private static readonly int[][] GradientStops =
    [
        [0xF7, 0x03, 0xF8],
        [0xB7, 0x5D, 0xF5],
        [0x83, 0xB4, 0xF5],
        [0x4B, 0xFA, 0xF3]
    ];

    private readonly record struct SeverityStyle(string Singular, string Plural, string Foreground, string Background);

    private static readonly Dictionary<string, SeverityStyle> SeverityStyles = new()
    {
        ["CRITICAL"] = new("Crítica", "Críticas", "#B42318", "#FEE4E2"),
        ["MAJOR"] = new("Mayor", "Mayores", "#B54708", "#FEF0C7"),
        ["MINOR"] = new("Menor", "Menores", "#175CD3", "#D1E9FF")
    };

    private static readonly byte[] LogoBytes;

    static OfficialReportRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.EnableDebugging = false;

        using var fontStream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("EBR.Infrastructure.Reports.Assets.AlexBrush-Regular.ttf")
            ?? throw new InvalidOperationException("No se encontró la fuente embebida de la firma visual (AlexBrush-Regular.ttf).");
        FontManager.RegisterFont(fontStream);

        using var logoStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(LogoResourceName)
            ?? throw new InvalidOperationException("No se encontró el logo embebido del informe (evalia-logo.png).");
        using var logoBuffer = new MemoryStream();
        logoStream.CopyTo(logoBuffer);
        LogoBytes = logoBuffer.ToArray();
    }

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
                page.MarginVertical(1.5f, Unit.Centimetre);
                page.MarginHorizontal(1.8f, Unit.Centimetre);
                page.DefaultTextStyle(style => style
                    .FontSize(10)
                    .FontFamily(Fonts.Arial)
                    .FontColor(Palette.Ink)
                    .LineHeight(1.3f));

                if (content.IsPreview)
                {
                    page.Foreground().AlignCenter().AlignMiddle().Rotate(-35)
                        .Text("NO OFICIAL").Bold().FontSize(64).FontColor(WatermarkColor);
                }

                page.Header().Element(header => ComposeHeader(header, content));
                page.Content().PaddingVertical(12).Column(body => ComposeBody(body, content));
                page.Footer().Element(footer => ComposeFooter(footer, content, contentHash));
            });
        });

        document.WithMetadata(new DocumentMetadata
        {
            Title = content.IsPreview
                ? $"Vista previa (no oficial) de la evaluación {content.EvaluationInstanceId} v{content.Version}"
                : $"Informe oficial de evaluación {content.EvaluationInstanceId} v{content.Version}",
            Author = "Sistema EBR/BPM",
            Subject = $"Expediente {content.CaseId}",
            Keywords = content.IsPreview ? "vista previa; no oficial" : contentHash,
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

    // ───────────────────────── Encabezado y pie ─────────────────────────

    private static void ComposeHeader(IContainer container, OfficialReportContent content)
    {
        container.PaddingBottom(8).Column(header =>
        {
            header.Item().Row(row =>
            {
                row.ConstantItem(HeaderLogoWidth).Image(LogoBytes).FitWidth();
                row.RelativeItem().AlignRight().AlignMiddle().Column(reference =>
                {
                    reference.Item().AlignRight()
                        .Text(content.IsPreview ? "Vista previa · No oficial" : "Informe oficial de evaluación BPM")
                        .Bold().FontSize(9).FontColor(content.IsPreview ? Palette.Danger : Palette.Primary);
                    reference.Item().AlignRight().Text($"Expediente {content.CaseId}")
                        .FontSize(8).FontColor(Palette.Muted);
                });
            });
            header.Item().PaddingTop(6).Element(bar => GradientBar(bar, 3));
        });
    }

    private static void ComposeFooter(IContainer container, OfficialReportContent content, string contentHash)
    {
        container.PaddingTop(6).Column(footer =>
        {
            footer.Item().LineHorizontal(0.5f).LineColor(Palette.Line);

            footer.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text(text =>
                {
                    text.DefaultTextStyle(FooterStyle);
                    text.Span("Documento generado el ");
                    text.Span(Format(content.GeneratedAt));
                });
                row.ConstantItem(90).AlignRight().Text(text =>
                {
                    text.DefaultTextStyle(FooterStyle);
                    text.Span("Página ");
                    text.CurrentPageNumber();
                    text.Span(" de ");
                    text.TotalPages();
                });
            });

            // La vista previa no lleva hash: no es el documento que se firma ni el que se guarda, y un
            // hash impreso aquí podría confundirse con el del PDF oficial.
            footer.Item().Text(text =>
            {
                text.DefaultTextStyle(FooterStyle);
                if (content.IsPreview)
                {
                    text.Span("Vista previa sin validez oficial · No está emitida ni firmada electrónicamente");
                }
                else
                {
                    text.Span("Hash de contenido: ");
                    text.Span(contentHash);
                }
            });
        });
    }

    private static TextStyle FooterStyle(TextStyle style) =>
        style.FontSize(7.5f).FontColor(Palette.Muted);

    // ───────────────────────── Cuerpo ─────────────────────────

    private static void ComposeBody(ColumnDescriptor body, OfficialReportContent content)
    {
        body.Spacing(16);

        TitleBlock(body, content);
        StatusBlock(body, content);

        if (!string.IsNullOrWhiteSpace(content.ReviewObservations))
            Section(body, "Observaciones del coordinador", content.ReviewObservations);

        Section(body, "Resumen ejecutivo", content.ExecutiveSummary);
        Section(body, "Hallazgos", content.Findings);

        ResultBlock(body, content);

        if (content.NonConformities.Count > 0)
            NonConformitiesTable(body, content);

        Section(body, "Recomendaciones", content.Recommendations);

        EvidenceBlock(body, content);

        SignatureBlock(body, content);
    }

    private static void TitleBlock(ColumnDescriptor column, OfficialReportContent content)
    {
        column.Item().Column(block =>
        {
            block.Item().Background(Palette.Primary).Padding(16).Column(title =>
            {
                title.Item()
                    .Text(content.IsPreview
                        ? "VISTA PREVIA DEL INFORME DE EVALUACIÓN BPM"
                        : "INFORME OFICIAL DE EVALUACIÓN BPM")
                    .Bold().FontSize(16).FontColor(Palette.White);
                title.Item().PaddingTop(6).Text(content.CompanyName)
                    .SemiBold().FontSize(12).FontColor(Palette.White);
                title.Item().Text($"RNC {content.CompanyRnc}")
                    .FontSize(9).FontColor(Palette.OnPrimarySoft);
            });
            block.Item().Element(bar => GradientBar(bar, 4));

            block.Item().PaddingTop(6).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(120);
                    columns.RelativeColumn();
                });

                void InfoRow(string label, string value)
                {
                    table.Cell().Element(InfoCell).Text(label).FontSize(9).SemiBold().FontColor(Palette.Muted);
                    table.Cell().Element(InfoCell).Text(value).FontSize(9);
                }

                InfoRow("Expediente", $"{content.CaseId}");
                InfoRow("Evaluación", $"{content.EvaluationInstanceId}");
                InfoRow("Versión del informe", $"{content.Version}");
                InfoRow("Fecha de emisión", Format(content.ReportIssuedAt));
            });
        });
    }

    /// <summary>
    /// Sello de estado del documento. El PDF oficial siempre sale APROBADO, con quien aprobó y cuándo; la
    /// vista previa muestra el estado que tenga la versión (pendiente, devuelta o con corrección
    /// solicitada) y un aviso de que no tiene validez oficial.
    /// </summary>
    private static void StatusBlock(ColumnDescriptor column, OfficialReportContent content)
    {
        var label = StatusLabel(content);
        var (foreground, background) = StatusColors(label);

        column.Item().Column(status =>
        {
            status.Spacing(8);

            status.Item().Row(row =>
            {
                row.AutoItem().Background(background).PaddingVertical(5).PaddingHorizontal(12)
                    .Text(label).Bold().FontSize(11).FontColor(foreground);

                row.RelativeItem().PaddingLeft(10).AlignMiddle().Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontSize(9).FontColor(Palette.Muted));
                    if (IsApproved(content))
                    {
                        text.Span("Aprobado por ");
                        text.Span(content.ApproverFullName).SemiBold();
                        text.Span($" el {Format(content.ApprovedAt)}");
                    }
                    else
                    {
                        text.Span("Estado de esta versión del informe en la revisión del coordinador.");
                    }
                });
            });

            if (content.IsPreview)
            {
                status.Item().Background(Palette.WarningBackground).Padding(8)
                    .Text("Vista previa sin validez oficial. Este documento no está emitido ni firmado electrónicamente; " +
                          "sirve solo para revisar el informe antes de decidir.")
                    .FontSize(9).FontColor(Palette.WarningText);
            }
        });
    }

    private static string StatusLabel(OfficialReportContent content) =>
        content.ReviewStatusLabel ?? OfficialReportStatusLabels.Approved;

    private static bool IsApproved(OfficialReportContent content) =>
        string.Equals(StatusLabel(content), OfficialReportStatusLabels.Approved, StringComparison.Ordinal);

    private static (string Foreground, string Background) StatusColors(string label) => label switch
    {
        OfficialReportStatusLabels.Approved => ("#067647", "#DCFAE6"),
        OfficialReportStatusLabels.Pending => ("#B54708", "#FEF0C7"),
        _ => ("#B42318", "#FEE4E2")
    };

    private static void ResultBlock(ColumnDescriptor column, OfficialReportContent content)
    {
        column.Item().Column(result =>
        {
            result.Spacing(10);
            result.Item().Element(container => SectionTitle(container, "Resultado BPM y de riesgo"));

            result.Item().ShowEntire().Row(row =>
            {
                row.Spacing(8);
                row.RelativeItem().Element(card =>
                    Kpi(card, "Cumplimiento BPM", $"{Number(content.BpmPercentage)}%", null));
                row.RelativeItem().Element(card =>
                    Kpi(card, "Calificación", $"{content.QualificationCode}", $"{content.Classification}"));
                row.RelativeItem().Element(card =>
                    Kpi(card, "Puntaje de riesgo", Number(content.BpmRiskScore), null));
                row.RelativeItem().Element(card =>
                    Kpi(card, "Inspección cada", $"{content.FrequencyMonths}", "meses"));
            });

            result.Item().Column(counts =>
            {
                counts.Spacing(6);
                counts.Item().Text("No conformidades por severidad").SemiBold().FontSize(10).FontColor(Palette.Primary);
                counts.Item().Row(row =>
                {
                    row.Spacing(8);
                    row.RelativeItem().Element(chip => CountChip(chip, "CRITICAL", $"{content.CriticalCount}"));
                    row.RelativeItem().Element(chip => CountChip(chip, "MAJOR", $"{content.MajorCount}"));
                    row.RelativeItem().Element(chip => CountChip(chip, "MINOR", $"{content.MinorCount}"));
                });
            });
        });
    }

    private static void Kpi(IContainer container, string label, string value, string? caption)
    {
        container
            .Background(Palette.Surface)
            .BorderLeft(3).BorderColor(Palette.Accent)
            .Padding(10)
            .Column(card =>
            {
                card.Item().Text(label).FontSize(8).SemiBold().FontColor(Palette.Muted);
                card.Item().PaddingTop(2).Text(value).FontSize(20).Bold().FontColor(Palette.Primary);
                if (!string.IsNullOrWhiteSpace(caption))
                    card.Item().Text(caption).FontSize(8).FontColor(Palette.Muted);
            });
    }

    private static void CountChip(IContainer container, string severityKey, string count)
    {
        var style = StyleFor(severityKey);
        container
            .Background(style.Background)
            .Padding(8)
            .Column(chip =>
            {
                chip.Item().Text(style.Plural).FontSize(8).SemiBold().FontColor(style.Foreground);
                chip.Item().Text(count).FontSize(16).Bold().FontColor(style.Foreground);
            });
    }

    private static void NonConformitiesTable(ColumnDescriptor column, OfficialReportContent content)
    {
        column.Item().Column(detail =>
        {
            detail.Spacing(8);
            detail.Item().Element(container => SectionTitle(container, "Detalle de no conformidades"));

            detail.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(64);
                    columns.ConstantColumn(72);
                    columns.RelativeColumn();
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("Severidad").FontSize(9).Bold().FontColor(Palette.White);
                    header.Cell().Element(HeaderCell).Text("Criterio").FontSize(9).Bold().FontColor(Palette.White);
                    header.Cell().Element(HeaderCell).Text("Descripción").FontSize(9).Bold().FontColor(Palette.White);
                });

                var index = 0;
                foreach (var group in OrderedGroups(content.NonConformities))
                {
                    var style = StyleFor(group.Key);
                    foreach (var item in group)
                    {
                        var rowColor = index++ % 2 == 0 ? Palette.White : Palette.Surface;

                        table.Cell().Background(style.Background).Element(BodyCell)
                            .Text(style.Singular).FontSize(8.5f).Bold().FontColor(style.Foreground);
                        table.Cell().Background(rowColor).Element(BodyCell)
                            .Text(item.CriterionCode).FontSize(9).SemiBold();
                        table.Cell().Background(rowColor).Element(BodyCell)
                            .Text(item.Description).FontSize(9);
                    }
                }
            });
        });
    }

    private static void EvidenceBlock(ColumnDescriptor column, OfficialReportContent content)
    {
        column.Item().Column(evidence =>
        {
            evidence.Spacing(8);
            evidence.Item().Element(container => SectionTitle(container, "Referencias de evidencias"));

            if (content.Evidences.Count == 0)
            {
                evidence.Item().Text("Sin evidencias registradas.").FontColor(Palette.Muted);
                return;
            }

            evidence.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.ConstantColumn(58);
                    columns.RelativeColumn(3);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("Archivo").FontSize(9).Bold().FontColor(Palette.White);
                    header.Cell().Element(HeaderCell).Text("Tamaño").FontSize(9).Bold().FontColor(Palette.White);
                    header.Cell().Element(HeaderCell).Text("SHA-256").FontSize(9).Bold().FontColor(Palette.White);
                });

                var index = 0;
                foreach (var item in content.Evidences.OrderBy(value => value.FileName, StringComparer.Ordinal))
                {
                    var rowColor = index++ % 2 == 0 ? Palette.White : Palette.Surface;

                    table.Cell().Background(rowColor).Element(BodyCell).Text(item.FileName).FontSize(9);
                    table.Cell().Background(rowColor).Element(BodyCell).Text(FormatSize(item.SizeBytes)).FontSize(9);
                    table.Cell().Background(rowColor).Element(BodyCell).Text(item.Sha256).FontSize(7).FontColor(Palette.Muted);
                }
            });
        });
    }

    /// <summary>
    /// Las dos firmas visuales del expediente (RF-19), al estilo de un documento firmado
    /// electrónicamente (p. ej. Adobe Sign): el técnico que emitió la versión y el coordinador que la
    /// aprobó, cada uno con la rúbrica que escribió impresa en cursiva y su nombre registrado debajo a
    /// manera de aclaración. Que sean dos responde al recorrido del informe —quien levanta el acta y
    /// quien la valida—, y la rúbrica no identifica a nadie por sí sola: la aclaración sale del usuario
    /// autenticado. No es la firma criptográfica —esa vive en el metadato, ver <c>IDocumentSigner</c>—,
    /// es la representación visual esperada en un documento oficial. En la vista previa, mientras el
    /// coordinador no ha aprobado, su recuadro queda como "Pendiente de decisión".
    /// </summary>
    private static void SignatureBlock(ColumnDescriptor column, OfficialReportContent content)
    {
        var coordinatorSigned = !content.IsPreview || IsApproved(content);

        column.Item().ShowEntire().PaddingTop(8).Column(block =>
        {
            block.Item().Element(container => SectionTitle(container, "Firmas electrónicas"));
            block.Item().PaddingTop(10).Row(row =>
            {
                Signer(row.RelativeItem(), content.TechnicianSignatureName, content.TechnicianFullName,
                    "Técnico Evaluador", "Emitido", content.ReportIssuedAt);
                row.ConstantItem(16);
                Signer(row.RelativeItem(),
                    coordinatorSigned ? content.ApproverSignatureName : null,
                    coordinatorSigned ? content.ApproverFullName : "Sin firmar",
                    "Coordinador · Evaluación Basada en Riesgo", "Aprobado",
                    coordinatorSigned ? content.ApprovedAt : null);
            });
        });
    }

    private static void Signer(
        IContainer container, string? rubric, string fullName, string role, string label, DateTimeOffset? signedAt)
    {
        container
            .Background(Palette.Surface)
            .Border(0.5f).BorderColor(Palette.Line)
            .Padding(12)
            .Column(signature =>
            {
                if (rubric is null)
                {
                    signature.Item().Height(40).AlignMiddle().PaddingLeft(6)
                        .Text("Pendiente de decisión").Italic().FontSize(11).FontColor(Palette.Muted);
                }
                else
                {
                    signature.Item().PaddingLeft(6).Text(rubric)
                        .FontFamily(SignatureFontFamily).FontSize(26).FontColor(Palette.Primary);
                }

                signature.Item().PaddingTop(2).LineHorizontal(0.75f).LineColor(Palette.Muted);
                signature.Item().PaddingTop(5).Text(fullName).FontSize(9).SemiBold();
                signature.Item().Text(role).FontSize(8).FontColor(Palette.Muted);
                if (signedAt is { } moment)
                    signature.Item().Text($"{label} {Format(moment)}").FontSize(7.5f).FontColor(Palette.Muted);
            });
    }

    // ───────────────────────── Piezas reutilizables ─────────────────────────

    private static void Section(ColumnDescriptor column, string title, string text)
    {
        column.Item().Column(section =>
        {
            section.Spacing(6);
            section.Item().Element(container => SectionTitle(container, title));
            section.Item().Text(string.IsNullOrWhiteSpace(text) ? "—" : text);
        });
    }

    private static void SectionTitle(IContainer container, string title)
    {
        container
            .BorderLeft(4).BorderColor(Palette.Accent)
            .PaddingLeft(8)
            .Text(title).Bold().FontSize(12).FontColor(Palette.Primary);
    }

    /// <summary>
    /// Franja con el degradado del logo (magenta → violeta → azul → cian), dibujada como tramos de color
    /// contiguos: solo usa rellenos planos, así que es igual de determinista que el resto del documento.
    /// </summary>
    private static void GradientBar(IContainer container, float height)
    {
        container.Row(row =>
        {
            for (var step = 0; step < GradientSteps; step++)
                row.RelativeItem().Height(height).Background(GradientColor(step / (double)(GradientSteps - 1)));
        });
    }

    private static string GradientColor(double position)
    {
        var scaled = Math.Clamp(position, 0d, 1d) * (GradientStops.Length - 1);
        var index = Math.Min((int)scaled, GradientStops.Length - 2);
        var local = scaled - index;
        var start = GradientStops[index];
        var end = GradientStops[index + 1];

        int Mix(int channel) => (int)Math.Round(start[channel] + (end[channel] - start[channel]) * local);

        return $"#{Mix(0):X2}{Mix(1):X2}{Mix(2):X2}";
    }

    private static IContainer InfoCell(IContainer container) =>
        container.BorderBottom(0.5f).BorderColor(Palette.Line).PaddingVertical(4);

    private static IContainer HeaderCell(IContainer container) =>
        container.Background(Palette.Primary).PaddingVertical(5).PaddingHorizontal(6);

    private static IContainer BodyCell(IContainer container) =>
        container.BorderBottom(0.5f).BorderColor(Palette.Line).PaddingVertical(5).PaddingHorizontal(6);

    private static SeverityStyle StyleFor(string severity) =>
        SeverityStyles.TryGetValue(severity, out var style)
            ? style
            : new SeverityStyle(severity, severity, Palette.Muted, Palette.Surface);

    private static IEnumerable<IGrouping<string, OfficialReportNonConformity>> OrderedGroups(
        IReadOnlyList<OfficialReportNonConformity> items)
    {
        var rank = new Dictionary<string, int> { ["CRITICAL"] = 0, ["MAJOR"] = 1, ["MINOR"] = 2 };
        return items
            .OrderBy(item => rank.GetValueOrDefault(item.Severity, 9))
            .ThenBy(item => item.CriterionCode, StringComparer.Ordinal)
            .GroupBy(item => item.Severity);
    }

    // ───────────────────────── Hash de contenido (sin cambios) ─────────────────────────

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

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{(bytes / 1024d).ToString("0.#", CultureInfo.InvariantCulture)} KB",
        _ => $"{(bytes / (1024d * 1024d)).ToString("0.##", CultureInfo.InvariantCulture)} MB"
    };
}
