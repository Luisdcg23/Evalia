using EBR.Domain.RiskCatalogs;
using EBR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EBR.Infrastructure.Risk;

/// <summary>
/// Resuelve a qué versión de reglas de riesgo se engancha cada alta hecha desde <c>/api/catalogs</c>.
/// <para>
/// El motor de cálculo (<see cref="RiskCalculationService"/>) solo trabaja contra una
/// <see cref="RiskRuleVersion"/> publicada: escalas de producto y de frecuencia, factores del
/// establecimiento y bandas de frecuencia pertenecen a una versión concreta y, una vez usada por un
/// cálculo, esa versión no debe cambiar. Los endpoints de catálogo creaban filas sin versión, lo que
/// obligaba a una ruta de cálculo alterna sobre <c>puntaje_total</c>; este proveedor elimina esa ruta.
/// </para>
/// <para>
/// <strong>Por qué hay dos destinos distintos</strong>: una versión publicada exige exactamente
/// <see cref="EbrDbContext.PublishedRuleFactorCount"/> factores activos cuyos pesos sumen 1
/// (invariante normativa, también verificada en <see cref="EbrDbContext"/>). Por eso un factor nuevo
/// no puede entrar en la versión publicada —la dejaría inconsistente— y va a un borrador
/// (<see cref="FactorDraftAsync"/>), mientras que los peligros de una subcategoría y las bandas de
/// frecuencia sí deben entrar en la versión con la que se calcula (<see cref="CatalogTargetAsync"/>),
/// porque de lo contrario el alimento o el intervalo recién dados de alta quedarían invisibles para el
/// cálculo.
/// </para>
/// <para>
/// <strong>Correspondencia de puntajes</strong>: para los niveles creados por API el puntaje del nivel
/// de escala es el mismo <see cref="RiskLevel.Points"/> que declara el administrador. No se introduce
/// ninguna tabla de conversión: los puntajes normativos de la matriz oficial los carga
/// <see cref="RiskCatalogSeeder"/> desde su fuente, no estos endpoints.
/// </para>
/// </summary>
public sealed class RiskRuleVersionProvisioner(EbrDbContext context)
{
    /// <summary>
    /// Versión con la que se calcula el riesgo, o <c>null</c> si todavía no se ha publicado ninguna.
    /// Publicar una versión es un acto normativo (carga del catálogo oficial con
    /// <see cref="RiskCatalogSeeder"/>), así que este proveedor nunca publica por su cuenta.
    /// </summary>
    public async Task<RiskRuleVersion?> FindPublishedAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        return await context.RiskRuleVersions
            .Where(version => version.IsActive && version.IsPublished && version.EffectiveFrom <= now &&
                (version.EffectiveTo == null || version.EffectiveTo > now))
            .OrderByDescending(version => version.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Destino de las altas de catálogo que el cálculo debe ver de inmediato (peligros de subcategorías
    /// y bandas de frecuencia): la versión publicada vigente si existe y, mientras no exista, el
    /// borrador en curso.
    /// </summary>
    public async Task<RiskRuleVersion> CatalogTargetAsync(CancellationToken cancellationToken) =>
        await FindPublishedAsync(cancellationToken) ?? await FactorDraftAsync(cancellationToken);

    /// <summary>
    /// Borrador que admite altas de factores del establecimiento. Reutiliza el borrador abierto si lo
    /// hay; si no, abre el siguiente número de versión heredando las escalas de la última versión para
    /// no duplicar catálogos.
    /// </summary>
    public async Task<RiskRuleVersion> FactorDraftAsync(CancellationToken cancellationToken)
    {
        var draft = await context.RiskRuleVersions
            .Where(version => version.IsActive && !version.IsPublished)
            .OrderByDescending(version => version.Version)
            .FirstOrDefaultAsync(cancellationToken);
        if (draft is not null) return draft;

        var latest = await context.RiskRuleVersions
            .OrderByDescending(version => version.Version)
            .FirstOrDefaultAsync(cancellationToken);
        var productScaleId = latest?.ProductScaleId ??
            (await EnsureScaleAsync(RiskCatalogSeeder.ProductScaleCode, "Riesgo del alimento", cancellationToken)).Id;
        var frequencyScaleId = latest?.FrequencyScaleId ??
            (await EnsureScaleAsync(RiskCatalogSeeder.FrequencyScaleCode, "Nivel descriptivo de frecuencia", cancellationToken)).Id;

        draft = new RiskRuleVersion
        {
            Version = (latest?.Version ?? 0) + 1,
            EffectiveFrom = DateTimeOffset.UtcNow,
            ProductScaleId = productScaleId,
            FrequencyScaleId = frequencyScaleId,
            IsPublished = false
        };
        context.RiskRuleVersions.Add(draft);
        await context.SaveChangesAsync(cancellationToken);
        return draft;
    }

    /// <summary>
    /// Devuelve el nivel de la escala de producto que representa a un <see cref="RiskLevel"/>,
    /// creándolo si el nivel se dio de alta por API después de la escala.
    /// </summary>
    public Task<RiskScaleLevel> ProductLevelAsync(RiskRuleVersion version, RiskLevel level, CancellationToken cancellationToken) =>
        EnsureLevelAsync(version.ProductScaleId, level, cancellationToken);

    public Task<RiskScaleLevel> FrequencyLevelAsync(RiskRuleVersion version, RiskLevel level, CancellationToken cancellationToken) =>
        EnsureLevelAsync(version.FrequencyScaleId, level, cancellationToken);

    private async Task<RiskScaleLevel> EnsureLevelAsync(int scaleId, RiskLevel level, CancellationToken cancellationToken)
    {
        var code = CodeOf(level);
        var existing = await context.RiskScaleLevels
            .SingleOrDefaultAsync(item => item.ScaleId == scaleId && item.Code == code, cancellationToken);
        if (existing is not null) return existing;

        existing = new RiskScaleLevel
        {
            ScaleId = scaleId,
            Code = code,
            Name = level.Name,
            Score = level.Points,
            Rank = level.Points
        };
        context.RiskScaleLevels.Add(existing);
        await context.SaveChangesAsync(cancellationToken);
        return existing;
    }

    private async Task<RiskScale> EnsureScaleAsync(string code, string name, CancellationToken cancellationToken)
    {
        var scale = await context.RiskScales
            .Where(item => item.Code == code)
            .OrderByDescending(item => item.Version)
            .FirstOrDefaultAsync(cancellationToken);
        if (scale is not null) return scale;

        scale = new RiskScale
        {
            Code = code,
            Name = name,
            Version = 1,
            EffectiveFrom = DateTimeOffset.UtcNow
        };
        context.RiskScales.Add(scale);
        await context.SaveChangesAsync(cancellationToken);
        return scale;
    }

    /// <summary>
    /// Código estable del nivel de escala derivado del identificador del <see cref="RiskLevel"/>: evita
    /// chocar con los códigos normativos (<c>LOW</c>, <c>MEDIUM</c>, <c>HIGH</c>) que carga el seeder.
    /// </summary>
    private static string CodeOf(RiskLevel level) => $"NIVEL-{level.Id}";
}
