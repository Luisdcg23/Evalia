using System.Security.Cryptography;
using EBR.Application.Reports;
using EBR.Domain.Evaluations;
using EBR.Infrastructure.Reports;

namespace EBR.UnitTests;

/// <summary>
/// PDF oficial del informe (RF-19). El renderizador solo compone datos ya persistidos y su salida es
/// determinista: el mismo contenido produce el mismo PDF y el mismo hash de contenido salvo la fecha
/// de generación incrustada como metadato.
/// </summary>
public sealed class OfficialReportRendererTests
{
    private static OfficialReportContent SampleContent(DateTimeOffset generatedAt) => new(
        EvaluationInstanceId: 42,
        CaseId: 7,
        CompanyName: "Lácteos del Valle SRL",
        CompanyRnc: "130000001",
        Version: 2,
        ExecutiveSummary: "El establecimiento cumple parcialmente con las condiciones evaluadas.",
        Findings: "Ausencia de registros de limpieza en el área de empaque.",
        Recommendations: "Implementar bitácora diaria de limpieza y capacitar al personal.",
        BpmPercentage: 78.50m,
        QualificationCode: "B",
        Classification: "ACEPTABLE",
        BpmRiskScore: 2.33m,
        FrequencyMonths: 12,
        CriticalCount: 1,
        MajorCount: 2,
        MinorCount: 3,
        NonConformities:
        [
            new(EvaluationCriticalityLevels.Critical, "1.2.3", "No se registran las temperaturas de conservación."),
            new(EvaluationCriticalityLevels.Major, "2.1.1", "Falta señalización de rutas de evacuación."),
            new(EvaluationCriticalityLevels.Minor, "3.4.2", "Pintura descascarada en pasillo secundario.")
        ],
        Evidences:
        [
            new("area-empaque.jpg", new string('a', 64), 240_512),
            new("acta-visita.pdf", new string('b', 64), 90_112)
        ],
        ReportIssuedAt: new DateTimeOffset(2026, 9, 1, 14, 30, 0, TimeSpan.Zero),
        GeneratedAt: generatedAt,
        ApproverFullName: "Coordinadora de Prueba");

    private static readonly OfficialReportRenderer Renderer = new();

    [Fact]
    public void RenderProducesANonEmptyPdf()
    {
        var rendered = Renderer.Render(SampleContent(DateTimeOffset.UtcNow));

        Assert.NotNull(rendered.Content);
        Assert.True(rendered.Content.Length > 500);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(rendered.Content, 0, 5));
        Assert.Equal(64, rendered.ContentSha256.Length);
    }

    [Fact]
    public void SameContentProducesTheSameContentHashAndBytes()
    {
        var first = Renderer.Render(SampleContent(new DateTimeOffset(2026, 9, 10, 8, 0, 0, TimeSpan.Zero)));
        var second = Renderer.Render(SampleContent(new DateTimeOffset(2026, 9, 10, 8, 0, 0, TimeSpan.Zero)));

        Assert.Equal(first.ContentSha256, second.ContentSha256);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(first.Content)),
            Convert.ToHexString(SHA256.HashData(second.Content)));
    }

    [Fact]
    public void GenerationDateDoesNotChangeTheContentHash()
    {
        var morning = Renderer.Render(SampleContent(new DateTimeOffset(2026, 9, 10, 8, 0, 0, TimeSpan.Zero)));
        var evening = Renderer.Render(SampleContent(new DateTimeOffset(2027, 1, 4, 22, 45, 0, TimeSpan.Zero)));

        Assert.Equal(morning.ContentSha256, evening.ContentSha256);
    }

    [Fact]
    public void ChangingReportedDataChangesTheContentHash()
    {
        var baseline = Renderer.Render(SampleContent(DateTimeOffset.UtcNow));

        var changed = SampleContent(DateTimeOffset.UtcNow) with { Findings = "Otro hallazgo distinto." };
        Assert.NotEqual(baseline.ContentSha256, Renderer.Render(changed).ContentSha256);

        var fewerNonConformities = SampleContent(DateTimeOffset.UtcNow) with { NonConformities = [] };
        Assert.NotEqual(baseline.ContentSha256, Renderer.Render(fewerNonConformities).ContentSha256);

        var otherEvidence = SampleContent(DateTimeOffset.UtcNow) with { Evidences = [] };
        Assert.NotEqual(baseline.ContentSha256, Renderer.Render(otherEvidence).ContentSha256);

        var otherRisk = SampleContent(DateTimeOffset.UtcNow) with { BpmRiskScore = 8.00m };
        Assert.NotEqual(baseline.ContentSha256, Renderer.Render(otherRisk).ContentSha256);

        var otherApprover = SampleContent(DateTimeOffset.UtcNow) with { ApproverFullName = "Otro Coordinador" };
        Assert.NotEqual(baseline.ContentSha256, Renderer.Render(otherApprover).ContentSha256);
    }
}
