using EBR.Domain.Evaluations;
using EBR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EBR.Infrastructure.Evaluations;

/// <summary>
/// Carga de forma idempotente la Ficha de Inspección BPM completa: el árbol de capítulos,
/// secciones, subsecciones, agrupadores y preguntas, las opciones evaluables C/CP/IT/NA,
/// los criterios de la guía de llenado y las bandas de calificación. Al terminar publica la
/// versión, que a partir de ese momento es inmutable.
/// </summary>
public sealed class BpmTemplateSeeder(EbrDbContext context)
{
    public async Task<BpmTemplateSeedResult> SeedAsync(CancellationToken cancellationToken)
    {
        Validate();
        var existing = await context.EvaluationTemplates
            .SingleOrDefaultAsync(template => template.Name == BpmTemplateData.TemplateName, cancellationToken);
        if (existing is not null)
        {
            return new BpmTemplateSeedResult(
                existing.Id,
                false,
                await context.EvaluationTemplateItems.CountAsync(item => item.TemplateId == existing.Id, cancellationToken),
                await context.EvaluationGuidanceCriteria.CountAsync(cancellationToken),
                await context.EvaluationResponseOptions.CountAsync(option => option.TemplateId == existing.Id, cancellationToken));
        }

        var template = new EvaluationTemplate { Name = BpmTemplateData.TemplateName };
        context.EvaluationTemplates.Add(template);
        await context.SaveChangesAsync(cancellationToken);

        var identifiers = await AddItemsAsync(template.Id, cancellationToken);
        await AddOptionsAsync(template.Id, cancellationToken);
        await AddQualificationBandsAsync(template.Id, cancellationToken);
        var criteria = await AddGuidanceAsync(identifiers, cancellationToken);

        template.Status = EvaluationTemplateStatuses.Published;
        template.PublishedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return new BpmTemplateSeedResult(template.Id, true, identifiers.Count, criteria, BpmResponseOptions.All.Count);
    }

    /// <summary>
    /// Comprueba las invariantes del contenido antes de escribir: códigos únicos, padres
    /// existentes, ausencia de ciclos, textos presentes y criterios asociados a un nodo real.
    /// </summary>
    private static void Validate()
    {
        var byCode = new Dictionary<string, BpmTemplateNode>(StringComparer.Ordinal);
        foreach (var node in BpmTemplateData.Nodes)
        {
            if (!byCode.TryAdd(node.Code, node))
                throw new InvalidOperationException($"El código {node.Code} está repetido en la ficha.");
            if (string.IsNullOrWhiteSpace(node.Description))
                throw new InvalidOperationException($"El nodo {node.Code} no tiene descripción en la ficha.");
            if (!EvaluationItemTypes.All.Contains(node.ItemType))
                throw new InvalidOperationException($"El nodo {node.Code} declara un tipo desconocido: {node.ItemType}.");
        }

        foreach (var node in BpmTemplateData.Nodes)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal) { node.Code };
            var current = node;
            while (current.ParentCode is not null)
            {
                if (!byCode.TryGetValue(current.ParentCode, out var parent))
                    throw new InvalidOperationException($"El padre {current.ParentCode} del nodo {current.Code} no existe en la ficha.");
                if (!visited.Add(parent.Code))
                    throw new InvalidOperationException($"El nodo {node.Code} participa en un ciclo del árbol.");
                current = parent;
            }
        }

        var criteriaCodes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var criterion in BpmTemplateData.Guidance)
        {
            if (!byCode.ContainsKey(criterion.ItemCode))
                throw new InvalidOperationException($"El criterio {criterion.Code} referencia el nodo inexistente {criterion.ItemCode}.");
            if (!criteriaCodes.Add($"{criterion.ItemCode}|{criterion.Code}"))
                throw new InvalidOperationException($"El criterio {criterion.ItemCode}.{criterion.Code} está repetido.");
            if (string.IsNullOrWhiteSpace(criterion.Description))
                throw new InvalidOperationException($"El criterio {criterion.ItemCode}.{criterion.Code} no tiene texto.");
        }
    }

    private async Task<Dictionary<string, int>> AddItemsAsync(int templateId, CancellationToken cancellationToken)
    {
        var identifiers = new Dictionary<string, int>(StringComparer.Ordinal);
        var order = 0;
        foreach (var node in BpmTemplateData.Nodes)
        {
            order += 10;
            var isQuestion = string.Equals(node.ItemType, BpmTemplateData.Question, StringComparison.Ordinal);
            var item = new EvaluationTemplateItem
            {
                TemplateId = templateId,
                ParentId = node.ParentCode is null ? null : identifiers[node.ParentCode],
                Code = node.Code,
                Description = node.Description,
                ItemType = node.ItemType,
                Order = order,
                Weight = isQuestion ? 1m : null,
                IsRequired = isQuestion,
                AllowsNotApplicable = isQuestion,
                ResponseType = isQuestion ? "BPM_OPTION" : null,
                RulesJson = $$"""{"hoja":"{{BpmTemplateData.FormSheet}}","celda":"{{node.SourceCell}}"}"""
            };
            context.EvaluationTemplateItems.Add(item);
            await context.SaveChangesAsync(cancellationToken);
            identifiers[node.Code] = item.Id;
        }

        return identifiers;
    }

    private async Task AddOptionsAsync(int templateId, CancellationToken cancellationToken)
    {
        var order = 0;
        foreach (var option in BpmResponseOptions.All)
        {
            order++;
            context.EvaluationResponseOptions.Add(new EvaluationResponseOption
            {
                TemplateId = templateId,
                Code = option.Code,
                Name = option.Name,
                Value = option.Value,
                CountsTowardDenominator = option.CountsTowardDenominator,
                Order = order
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task AddQualificationBandsAsync(int templateId, CancellationToken cancellationToken)
    {
        var order = 0;
        foreach (var band in BpmTemplateData.QualificationBands)
        {
            order++;
            context.EvaluationQualificationRules.Add(new EvaluationQualificationRule
            {
                TemplateId = templateId,
                Code = band.Code,
                Description = band.Description,
                Classification = band.Classification,
                Action = band.Action,
                MinPercentage = band.MinPercentage,
                MinIncluded = band.MinIncluded,
                MaxPercentage = band.MaxPercentage,
                MaxIncluded = band.MaxIncluded,
                Order = order
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<int> AddGuidanceAsync(Dictionary<string, int> identifiers, CancellationToken cancellationToken)
    {
        var orders = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var criterion in BpmTemplateData.Guidance)
        {
            orders.TryGetValue(criterion.ItemCode, out var order);
            order++;
            orders[criterion.ItemCode] = order;
            context.EvaluationGuidanceCriteria.Add(new EvaluationGuidanceCriterion
            {
                ItemId = identifiers[criterion.ItemCode],
                Code = criterion.Code,
                Description = criterion.Description,
                Criticality = EvaluationCriticalityLevels.FromFormMark(criterion.CriticalityMark),
                SourceSheet = BpmTemplateData.GuideSheet,
                SourceCell = criterion.SourceCell,
                Order = order
            });
        }

        await context.SaveChangesAsync(cancellationToken);
        return BpmTemplateData.Guidance.Count;
    }
}

public sealed record BpmTemplateSeedResult(int TemplateId, bool Created, int Items, int Criteria, int Options);
