using System.Globalization;
using System.Text.RegularExpressions;

namespace EBR.Infrastructure.Risk;

/// <summary>
/// Lee la extracción textual de la hoja de categorización de alimentos y separa las filas
/// aprovechables de las incompletas. Ninguna fila incompleta se completa por inferencia.
/// </summary>
public static partial class FoodRiskMatrixImporter
{
    private const string MissingMarker = "#N/A";
    private const int MinimumSubcategoryLength = 3;

    private static readonly Dictionary<string, decimal> ScaleByLevel = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
    {
        ["BAJO"] = 2m,
        ["MEDIO"] = 4m,
        ["ALTO"] = 8m
    };

    public static FoodRiskMatrixImportResult Import(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var accepted = new List<FoodRiskMatrixRow>();
        var rejected = new List<FoodRiskMatrixRejection>();
        foreach (var (rowNumber, cells) in ReadRows(source))
        {
            if (rowNumber == 1) continue;
            var category = Value(cells, "A");
            var subcategory = Value(cells, "B");
            var microbiologicalLevel = Value(cells, "C");
            var microbiologicalScore = Score(cells, "D");
            var chemicalLevel = Value(cells, "E");
            var chemicalScore = Score(cells, "F");
            var sourceTotalScore = Score(cells, "G");

            var reason = Reject(
                category,
                subcategory,
                microbiologicalLevel,
                microbiologicalScore,
                chemicalLevel,
                chemicalScore);
            if (reason is not null)
            {
                rejected.Add(new FoodRiskMatrixRejection(rowNumber, category, subcategory, reason));
                continue;
            }

            accepted.Add(new FoodRiskMatrixRow(
                rowNumber,
                category!,
                subcategory!,
                microbiologicalLevel!.ToUpperInvariant(),
                microbiologicalScore!.Value,
                chemicalLevel?.ToUpperInvariant(),
                chemicalScore,
                sourceTotalScore));
        }

        return new FoodRiskMatrixImportResult(accepted, rejected);
    }

    private static string? Reject(
        string? category,
        string? subcategory,
        string? microbiologicalLevel,
        decimal? microbiologicalScore,
        string? chemicalLevel,
        decimal? chemicalScore)
    {
        if (category is null) return "Categoría ausente.";
        if (subcategory is null) return "Subcategoría ausente.";
        if (subcategory.Length < MinimumSubcategoryLength) return "Subcategoría incompleta o marcador sin significado.";
        if (microbiologicalLevel is null || microbiologicalScore is null) return "Riesgo microbiológico ausente.";
        if (!ScaleByLevel.TryGetValue(microbiologicalLevel, out var expectedMicrobiological))
            return "Nivel de riesgo microbiológico desconocido.";
        if (expectedMicrobiological != microbiologicalScore) return "Puntaje microbiológico inconsistente con el nivel.";
        if (chemicalLevel is null && chemicalScore is not null) return "Puntaje químico sin nivel de riesgo.";
        if (chemicalLevel is null) return null;
        if (chemicalScore is null) return "Riesgo químico sin puntaje.";
        if (!ScaleByLevel.TryGetValue(chemicalLevel, out var expectedChemical)) return "Nivel de riesgo químico desconocido.";
        return expectedChemical == chemicalScore ? null : "Puntaje químico inconsistente con el nivel.";
    }

    private static IEnumerable<(int RowNumber, Dictionary<string, string> Cells)> ReadRows(string source)
    {
        var rows = new SortedDictionary<int, Dictionary<string, string>>();
        string? lastColumn = null;
        var lastRow = 0;
        foreach (var line in source.Split('\n'))
        {
            var text = line.Trim('\r');
            if (SheetHeaderPattern().IsMatch(text) || text.Trim().Length == 0) continue;
            foreach (var token in text.Split(" | "))
            {
                var match = CellPattern().Match(token);
                if (!match.Success)
                {
                    if (lastColumn is null) continue;
                    rows[lastRow][lastColumn] = $"{rows[lastRow][lastColumn]} {token.Trim()}".Trim();
                    continue;
                }

                lastColumn = match.Groups["column"].Value;
                lastRow = int.Parse(match.Groups["row"].Value, CultureInfo.InvariantCulture);
                if (!rows.TryGetValue(lastRow, out var cells)) rows[lastRow] = cells = [];
                cells[lastColumn] = match.Groups["value"].Value.Trim();
            }
        }

        return rows.Select(row => (row.Key, row.Value));
    }

    private static string? Value(Dictionary<string, string> cells, string column)
    {
        if (!cells.TryGetValue(column, out var value)) return null;
        var text = WhitespacePattern().Replace(value, " ").Trim();
        return text.Length == 0 || string.Equals(text, MissingMarker, StringComparison.OrdinalIgnoreCase) ? null : text;
    }

    private static decimal? Score(Dictionary<string, string> cells, string column) =>
        Value(cells, column) is { } text &&
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var score)
            ? score
            : null;

    [GeneratedRegex(@"^\s*=====\s*SHEET:")]
    private static partial Regex SheetHeaderPattern();

    [GeneratedRegex(@"^(?<column>[A-Z]{1,2})(?<row>\d+)=(?<value>.*)$", RegexOptions.Singleline)]
    private static partial Regex CellPattern();

    [GeneratedRegex(@"\s+", RegexOptions.Singleline)]
    private static partial Regex WhitespacePattern();
}

public sealed record FoodRiskMatrixRow(
    int RowNumber,
    string Category,
    string Subcategory,
    string MicrobiologicalLevel,
    decimal MicrobiologicalScore,
    string? ChemicalLevel,
    decimal? ChemicalScore,
    decimal? SourceTotalScore)
{
    /// <summary>
    /// Prevalece el mayor riesgo conocido. El total de la fuente se conserva solo como referencia
    /// porque la hoja original promedia los puntajes, inconsistencia registrada en <c>DATABASE.md</c>.
    /// </summary>
    public decimal ProductScore => ChemicalScore is { } chemical && chemical > MicrobiologicalScore
        ? chemical
        : MicrobiologicalScore;

    public bool SourceTotalDiffers => SourceTotalScore is not null && SourceTotalScore != ProductScore;
}

public sealed record FoodRiskMatrixRejection(int RowNumber, string? Category, string? Subcategory, string Reason);

public sealed record FoodRiskMatrixImportResult(
    IReadOnlyList<FoodRiskMatrixRow> Accepted,
    IReadOnlyList<FoodRiskMatrixRejection> Rejected);
