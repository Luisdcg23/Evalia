using System.Security.Cryptography;
using System.Text;
using EBR.Application.Reports;
using EBR.Domain.Evaluations;
using EBR.Infrastructure.Reports;

namespace EBR.UnitTests;

/// <summary>
/// Vista previa del PDF. La primera prueba es una utilidad de desarrollo: escribe PDFs de ejemplo a disco
/// para revisar el diseño a simple vista, y no hace nada (y pasa) salvo que se defina la variable de
/// entorno EBR_PDF_PREVIEW_DIR con la carpeta de salida. Las otras dos son pruebas normales del modo
/// vista previa del coordinador.
/// </summary>
public sealed class OfficialReportPreviewTests
{
    private const string OutputDirectoryVariable = "EBR_PDF_PREVIEW_DIR";

    [Fact]
    public void WritesPreviewPdfsWhenRequested()
    {
        var directory = Environment.GetEnvironmentVariable(OutputDirectoryVariable);
        if (string.IsNullOrWhiteSpace(directory))
            return;

        Directory.CreateDirectory(directory);
        var renderer = new OfficialReportRenderer();

        // PDF oficial (siempre APROBADO) en sus dos variantes.
        Write(renderer, directory, "informe-completo.pdf", FullContent());
        Write(renderer, directory, "informe-minimo.pdf", MinimalContent());

        // Vista previa del coordinador (marca de agua NO OFICIAL) según el estado de la versión.
        Write(renderer, directory, "vista-previa-pendiente.pdf", FullContent() with
        {
            IsPreview = true,
            ReviewStatusLabel = OfficialReportStatusLabels.Pending
        });
        Write(renderer, directory, "vista-previa-devuelto.pdf", FullContent() with
        {
            IsPreview = true,
            ReviewStatusLabel = OfficialReportStatusLabels.Returned,
            ReviewObservations =
                "Falta detallar en los hallazgos la ubicación exacta de la cámara de refrigeración y adjuntar la " +
                "evidencia fotográfica de los registros de temperatura."
        });
        Write(renderer, directory, "vista-previa-correccion.pdf", MinimalContent() with
        {
            IsPreview = true,
            ReviewStatusLabel = OfficialReportStatusLabels.CorrectionRequested,
            ReviewObservations = "Ampliar las recomendaciones con plazos concretos para cada acción."
        });
    }

    [Fact]
    public void PreviewModeDoesNotChangeTheContentHashButChangesTheDocument()
    {
        var renderer = new OfficialReportRenderer();

        var official = renderer.Render(FullContent());
        var preview = renderer.Render(FullContent() with
        {
            IsPreview = true,
            ReviewStatusLabel = OfficialReportStatusLabels.Pending
        });

        Assert.Equal("%PDF-", Encoding.ASCII.GetString(preview.Content, 0, 5));
        Assert.Equal(official.ContentSha256, preview.ContentSha256);
        Assert.NotEqual(
            Convert.ToHexString(SHA256.HashData(official.Content)),
            Convert.ToHexString(SHA256.HashData(preview.Content)));
    }

    [Fact]
    public void ReviewStatusAndObservationsDoNotChangeTheContentHash()
    {
        var renderer = new OfficialReportRenderer();

        var baseline = renderer.Render(FullContent());
        var returned = renderer.Render(FullContent() with
        {
            ReviewStatusLabel = OfficialReportStatusLabels.Returned,
            ReviewObservations = "Observación de prueba."
        });

        Assert.Equal(baseline.ContentSha256, returned.ContentSha256);
    }

    private static void Write(OfficialReportRenderer renderer, string directory, string fileName, OfficialReportContent content) =>
        File.WriteAllBytes(Path.Combine(directory, fileName), renderer.Render(content).Content);

    private static OfficialReportContent FullContent()
    {
        var nonConformities = new List<OfficialReportNonConformity>
        {
            new(EvaluationCriticalityLevels.Critical, "1.2.3", "No se registran las temperaturas de conservación de la cámara de refrigeración durante los últimos 30 días."),
            new(EvaluationCriticalityLevels.Major, "2.1.1", "Falta señalización de rutas de evacuación y de salidas de emergencia en el área de producción."),
            new(EvaluationCriticalityLevels.Major, "2.3.4", "Los lavamanos del área de empaque no cuentan con dispensador de jabón ni con secado higiénico."),
            new(EvaluationCriticalityLevels.Major, "4.1.2", "El programa de control de plagas no presenta registros de las inspecciones del último trimestre."),
            new(EvaluationCriticalityLevels.Minor, "3.4.2", "Pintura descascarada en el pasillo secundario, cerca del área de almacenamiento de insumos."),
            new(EvaluationCriticalityLevels.Minor, "3.5.1", "Iluminación insuficiente en dos puntos de la zona de recepción de materia prima."),
            new(EvaluationCriticalityLevels.Minor, "5.2.3", "Algunos envases de productos de limpieza no tienen etiqueta legible."),
            new(EvaluationCriticalityLevels.Minor, "6.1.1", "El registro de capacitación del personal no incluye la firma de todos los asistentes."),
            new(EvaluationCriticalityLevels.Minor, "6.3.2", "No se evidencia el plan de mantenimiento preventivo de los equipos de cocción."),
            new(EvaluationCriticalityLevels.Minor, "7.1.4", "Falta rotulación de las áreas de producto terminado y producto en cuarentena.")
        };

        string[] evidenceFiles =
        [
            "area-empaque-01.jpg",
            "camara-refrigeracion-temperaturas.jpg",
            "acta-visita-2026-09-01.pdf",
            "lavamanos-empaque.png",
            "registro-plagas-trimestre-3.pdf",
            "pasillo-secundario.webp"
        ];
        var evidences = evidenceFiles
            .Select((name, index) => new OfficialReportEvidence(
                name,
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name))).ToLowerInvariant(),
                85_000L + index * 412_345L))
            .ToList();

        return new OfficialReportContent(
            EvaluationInstanceId: 1042,
            CaseId: 317,
            CompanyName: "Industrias Lácteas del Valle del Cibao, S.R.L.",
            CompanyRnc: "130000001",
            Version: 2,
            ExecutiveSummary:
                "El establecimiento cumple parcialmente con las condiciones de Buenas Prácticas de Manufactura evaluadas. " +
                "Se observó un sistema de producción ordenado y personal con conocimiento de sus funciones, pero existen " +
                "brechas en el control documental y en la conservación de la cadena de frío que requieren atención inmediata.\n\n" +
                "La evaluación se realizó con la presencia del representante de calidad y cubrió las áreas de recepción, " +
                "producción, empaque, almacenamiento y servicios sanitarios.",
            Findings:
                "Durante la visita se identificaron diez no conformidades: una crítica, tres mayores y seis menores.\n\n" +
                "La no conformidad crítica corresponde a la ausencia de registros de temperatura en la cámara de refrigeración, " +
                "lo que impide demostrar que los productos perecederos se conservaron dentro del rango establecido. " +
                "Las no conformidades mayores se relacionan con la señalización de emergencia, la higiene de manos en el área " +
                "de empaque y la falta de evidencia del control de plagas.\n\n" +
                "Las no conformidades menores son de orden físico y documental y pueden resolverse en el corto plazo sin " +
                "inversión significativa.",
            Recommendations:
                "1. Implementar de inmediato una bitácora diaria de temperaturas en las cámaras de refrigeración, con responsable " +
                "designado y revisión semanal por parte de calidad.\n\n" +
                "2. Instalar dispensadores de jabón y secado higiénico en todos los lavamanos del área de empaque.\n\n" +
                "3. Colocar la señalización de rutas de evacuación y salidas de emergencia según la normativa vigente.\n\n" +
                "4. Solicitar al proveedor de control de plagas los informes de inspección del último trimestre y establecer " +
                "una frecuencia mensual de entrega.\n\n" +
                "5. Completar el plan de mantenimiento preventivo de equipos y actualizar los registros de capacitación.",
            BpmPercentage: 78.50m,
            QualificationCode: "B",
            Classification: "ACEPTABLE CON OBSERVACIONES",
            BpmRiskScore: 2.33m,
            FrequencyMonths: 12,
            CriticalCount: 1,
            MajorCount: 3,
            MinorCount: 6,
            NonConformities: nonConformities,
            Evidences: evidences,
            ReportIssuedAt: new DateTimeOffset(2026, 9, 1, 14, 30, 0, TimeSpan.Zero),
            GeneratedAt: new DateTimeOffset(2026, 9, 20, 10, 0, 0, TimeSpan.Zero),
            ApproverFullName: "María Fernanda Peña Rodríguez",
            ApproverSignatureName: "María F. Peña",
            ApprovedAt: new DateTimeOffset(2026, 9, 3, 9, 15, 0, TimeSpan.Zero),
            TechnicianFullName: "Luis Alberto Cabrera Santos",
            TechnicianSignatureName: "Luis A. Cabrera");
    }

    private static OfficialReportContent MinimalContent() => new(
        EvaluationInstanceId: 7,
        CaseId: 3,
        CompanyName: "Panadería El Sol",
        CompanyRnc: "101000002",
        Version: 1,
        ExecutiveSummary: "Sin observaciones relevantes.",
        Findings: "",
        Recommendations: "Mantener las prácticas actuales.",
        BpmPercentage: 96m,
        QualificationCode: "A",
        Classification: "SATISFACTORIO",
        BpmRiskScore: 1.1m,
        FrequencyMonths: 24,
        CriticalCount: 0,
        MajorCount: 0,
        MinorCount: 0,
        NonConformities: [],
        Evidences: [],
        ReportIssuedAt: new DateTimeOffset(2026, 9, 1, 14, 30, 0, TimeSpan.Zero),
        GeneratedAt: new DateTimeOffset(2026, 9, 20, 10, 0, 0, TimeSpan.Zero),
        ApproverFullName: "María Fernanda Peña Rodríguez",
        ApproverSignatureName: "María F. Peña",
        ApprovedAt: new DateTimeOffset(2026, 9, 3, 9, 15, 0, TimeSpan.Zero),
        TechnicianFullName: "Luis Alberto Cabrera Santos",
        TechnicianSignatureName: "Luis A. Cabrera");
}
