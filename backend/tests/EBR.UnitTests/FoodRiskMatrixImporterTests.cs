using EBR.Infrastructure.Risk;

namespace EBR.UnitTests;

public sealed class FoodRiskMatrixImporterTests
{
    internal const string Source = """
        ===== SHEET: Categorización_de_alimentos (1002x28) =====
        A1=CATEGORIA | B1=SUBCATEGORIA | C1=RIESGO MICROBIOLÓGICO | D1=PUNTAJE | E1=RIESGO QUÍMICO | F1=PUNTAJE | G1=RIESGO TOTAL
        A2=Productos lácteos | B2=Grasa láctea | C2=BAJO | D2=2 | G2=2
        D3=#N/A | G3=#N/A
        A4=Frutas y Hortalizas | B4=Frutas en conserva, enlatadas o en frascos | C4=BAJO | D4=2 | E4=MEDIO | F4=4 | G4=3
        A5=Frutas y Hortalizas | B5=Pulpas y preparados de hortalizas
        A6=Carne y productos cárnicos | B6=Crudo intacto | C6=ALTO | D6=8 | E6=ALTO | G6=8
        A7=Alimentos preparados | C7=BAJO | D7=2 | G7=2
        A8=Pescado y productos pesqueros | B8=p | C8=BAJO | D8=2 | G8=2
        A9=Pescado y productos pesqueros | B9=Pescado, filetes de pescado y productos pesqueros
        congelados | C9=MEDIO | D9=4 | G9=4
        """;

    [Fact]
    public void IncompleteRowsAreRejectedInsteadOfCompletedByInference()
    {
        var result = FoodRiskMatrixImporter.Import(Source);

        Assert.Equal([2, 4, 9], result.Accepted.Select(row => row.RowNumber));
        Assert.Equal(
            [
                (3, "Categoría ausente."),
                (5, "Riesgo microbiológico ausente."),
                (6, "Riesgo químico sin puntaje."),
                (7, "Subcategoría ausente."),
                (8, "Subcategoría incompleta o marcador sin significado.")
            ],
            result.Rejected.Select(row => (row.RowNumber, row.Reason)));
    }

    [Fact]
    public void AveragedSourceTotalDoesNotReplaceTheMaximumKnownHazard()
    {
        var row = FoodRiskMatrixImporter.Import(Source).Accepted.Single(item => item.RowNumber == 4);

        Assert.Equal(4m, row.ProductScore);
        Assert.Equal(3m, row.SourceTotalScore);
        Assert.True(row.SourceTotalDiffers);
    }

    [Fact]
    public void MultilineDescriptionsAreRebuiltWithoutLosingText()
    {
        var row = FoodRiskMatrixImporter.Import(Source).Accepted.Single(item => item.RowNumber == 9);

        Assert.Equal("Pescado, filetes de pescado y productos pesqueros congelados", row.Subcategory);
        Assert.Equal(4m, row.MicrobiologicalScore);
        Assert.Null(row.ChemicalScore);
    }
}
