using EBR.Application.Evaluations;
using EBR.Application.Risk;
using EBR.Domain.Evaluations;
using EBR.Domain.RiskCatalogs;
using EBR.Infrastructure.Persistence;
using EBR.Infrastructure.Risk;
using Microsoft.EntityFrameworkCore;

namespace EBR.Infrastructure.Evaluations;

/// <summary>
/// Integra el resultado BPM con el cálculo de riesgo (RF-14). El resultado se guarda como una
/// fotografía inmutable en <c>Evaluacion_Resultado</c>: se calcula una sola vez, en el envío, contra la
/// plantilla y la versión de reglas congeladas en la instancia. Publicar después otra versión de
/// reglas o de la ficha no altera evaluaciones ya enviadas.
/// </summary>
public sealed class EvaluationSubmissionService(
    EbrDbContext context,
    IRiskCalculationService riskCalculationService)
    : IEvaluationSubmissionService
{
    public async Task<string?> DescribeSubmissionBlockerAsync(int evaluationInstanceId, CancellationToken cancellationToken)
    {
        var instance = await context.EvaluationInstances.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == evaluationInstanceId, cancellationToken);
        if (instance is null) return "La evaluación no existe.";

        var inputs = await LoadAsync(instance, cancellationToken);
        try
        {
            var calculated = EvaluationSubmissionCalculator.Calculate(inputs.Questions, inputs.Answers, inputs.Criteria);
            var qualification = BpmQualificationSelector.Select(inputs.Rules, calculated.Score.Percentage!.Value);
            if (qualification is null)
            {
                return "La plantilla de la evaluación no declara una banda de calificación aplicable a " +
                    $"{calculated.Score.Percentage} %.";
            }

            return await DescribeRiskBlockerAsync(instance, inputs, qualification, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            return exception.Message;
        }
    }

    public async Task<EvaluationSubmissionOutcome> RegisterResultAsync(
        int evaluationInstanceId,
        Guid submittedBy,
        CancellationToken cancellationToken)
    {
        var instance = await context.EvaluationInstances.AsNoTracking()
            .SingleAsync(value => value.Id == evaluationInstanceId, cancellationToken);
        if (instance.Status != EvaluationInstanceStatuses.Submitted)
            throw new InvalidOperationException("El resultado solo se registra sobre una evaluación enviada.");

        var inputs = await LoadAsync(instance, cancellationToken);
        var calculated = EvaluationSubmissionCalculator.Calculate(inputs.Questions, inputs.Answers, inputs.Criteria);
        var percentage = calculated.Score.Percentage!.Value;
        var qualification = BpmQualificationSelector.Select(inputs.Rules, percentage)
            ?? throw new InvalidOperationException(
                $"La plantilla de la evaluación no declara una banda de calificación aplicable a {percentage} %.");

        var bpmOption = await BpmFactorOptionAsync(instance, inputs.Rules, qualification, cancellationToken);
        var risk = await riskCalculationService.CalculateAsync(
            new RiskCalculationCommand(
                inputs.CompanyId,
                await FactorSelectionsAsync(instance, inputs.CompanyId, bpmOption, cancellationToken),
                submittedBy,
                instance.RiskRuleVersionId),
            cancellationToken);

        var result = new EvaluationResult
        {
            EvaluationInstanceId = instance.Id,
            BpmPoints = calculated.Score.Points,
            BpmDenominator = calculated.Score.Denominator,
            BpmPercentage = percentage,
            QualificationCode = qualification.Code,
            Classification = qualification.Classification,
            BpmRiskScore = bpmOption.Score,
            CriticalCount = calculated.CriticalCount,
            MajorCount = calculated.MajorCount,
            MinorCount = calculated.MinorCount,
            RiskCalculationId = risk.Id,
            FrequencyMonths = risk.FrequencyMonths
        };
        context.EvaluationResults.Add(result);
        await context.SaveChangesAsync(cancellationToken);

        var responseByQuestion = inputs.ResponseIdByQuestionId;
        context.EvaluationNonConformities.AddRange(calculated.NonConformities.Select(item => new EvaluationNonConformity
        {
            EvaluationResultId = result.Id,
            EvaluationResponseId = responseByQuestion[item.QuestionId],
            GuidanceCriterionId = item.CriterionId,
            Severity = item.Severity
        }));
        await context.SaveChangesAsync(cancellationToken);

        return new EvaluationSubmissionOutcome(
            instance.Id,
            result.Id,
            result.BpmPercentage,
            result.QualificationCode,
            result.Classification,
            result.CriticalCount,
            result.MajorCount,
            result.MinorCount,
            result.RiskCalculationId);
    }

    /// <summary>
    /// Traduce la banda de calificación BPM a la opción del factor estructural <c>BPM</c> de la versión
    /// de reglas congelada. <strong>Decisión de modelado</strong>: las dos fuentes normativas describen
    /// el mismo eje (peor a mejor cumplimiento) con la misma cantidad de escalones pero con umbrales
    /// redactados distinto, así que la correspondencia se hace por posición ordinal y no reinterpretando
    /// porcentajes. Si el número de escalones no coincide, se rechaza en lugar de aproximar.
    /// </summary>
    private async Task<StructuralRiskOption> BpmFactorOptionAsync(
        EvaluationInstance instance,
        IReadOnlyList<EvaluationQualificationRule> rules,
        EvaluationQualificationRule qualification,
        CancellationToken cancellationToken)
    {
        var factor = await context.StructuralRiskFactors.AsNoTracking().Include(item => item.Options)
            .SingleOrDefaultAsync(item =>
                item.RuleVersionId == instance.RiskRuleVersionId &&
                item.IsActive &&
                item.Code == RiskCatalogSeeder.BpmFactorCode, cancellationToken)
            ?? throw new InvalidOperationException(
                $"La versión de reglas de riesgo no declara el factor {RiskCatalogSeeder.BpmFactorCode}.");

        var options = factor.Options.OrderBy(option => option.Order).ToArray();
        if (options.Length != rules.Count)
        {
            throw new InvalidOperationException(
                $"El factor {RiskCatalogSeeder.BpmFactorCode} tiene {options.Length} opciones y la ficha declara " +
                $"{rules.Count} bandas de calificación: no puede establecerse la correspondencia.");
        }

        var position = rules.ToList().IndexOf(qualification);
        return options[position];
    }

    /// <summary>
    /// Toma los valores vigentes de los factores del establecimiento y sustituye el factor <c>BPM</c>
    /// por el que acaba de determinar esta evaluación, que es precisamente lo que la evaluación aporta
    /// al perfil de riesgo de la empresa.
    /// </summary>
    private async Task<IReadOnlyList<RiskFactorSelection>> FactorSelectionsAsync(
        EvaluationInstance instance,
        int companyId,
        StructuralRiskOption bpmOption,
        CancellationToken cancellationToken)
    {
        var factors = await context.StructuralRiskFactors.AsNoTracking()
            .Where(item => item.RuleVersionId == instance.RiskRuleVersionId && item.IsActive)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        var current = await context.CompanyRiskFactorValues.AsNoTracking()
            .Where(value => value.CompanyId == companyId && value.IsCurrent && factors.Contains(value.FactorId))
            .ToDictionaryAsync(value => value.FactorId, value => value.OptionId, cancellationToken);
        current[bpmOption.FactorId] = bpmOption.Id;

        var missing = factors.Where(id => !current.ContainsKey(id)).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                "La empresa no tiene registrado un valor vigente para todos los factores del establecimiento " +
                $"de la versión de reglas ({missing.Length} sin registrar).");
        }

        return factors.Select(id => new RiskFactorSelection(id, current[id])).ToArray();
    }

    private async Task<string?> DescribeRiskBlockerAsync(
        EvaluationInstance instance,
        SubmissionInputs inputs,
        EvaluationQualificationRule qualification,
        CancellationToken cancellationToken)
    {
        try
        {
            var option = await BpmFactorOptionAsync(instance, inputs.Rules, qualification, cancellationToken);
            await FactorSelectionsAsync(instance, inputs.CompanyId, option, cancellationToken);
            return null;
        }
        catch (InvalidOperationException exception)
        {
            return exception.Message;
        }
    }

    private async Task<SubmissionInputs> LoadAsync(EvaluationInstance instance, CancellationToken cancellationToken)
    {
        var companyId = await context.InspectionCases.AsNoTracking()
            .Where(value => value.Id == instance.CaseId)
            .Select(value => value.CompanyId)
            .SingleAsync(cancellationToken);

        var items = await context.EvaluationTemplateItems.AsNoTracking()
            .Where(item => item.TemplateId == instance.TemplateId && item.IsActive &&
                item.ItemType == EvaluationItemTypes.Question)
            .ToListAsync(cancellationToken);
        var questions = items
            .Select(item => new EvaluationSubmissionQuestion(item.Id, item.Code, item.Weight ?? 1m, item.AllowsNotApplicable))
            .ToArray();

        var responses = await context.EvaluationResponses.AsNoTracking()
            .Where(value => value.EvaluationInstanceId == instance.Id)
            .ToListAsync(cancellationToken);

        var itemIds = items.Select(item => item.Id).ToList();
        var criteria = await context.EvaluationGuidanceCriteria.AsNoTracking()
            .Where(criterion => itemIds.Contains(criterion.ItemId))
            .Select(criterion => new EvaluationSubmissionCriterion(
                criterion.Id, criterion.ItemId, criterion.Code, criterion.Criticality))
            .ToListAsync(cancellationToken);

        var rules = await context.EvaluationQualificationRules.AsNoTracking()
            .Where(rule => rule.TemplateId == instance.TemplateId)
            .OrderBy(rule => rule.Order)
            .ToListAsync(cancellationToken);

        return new SubmissionInputs(
            companyId,
            questions,
            responses.Select(value => new EvaluationSubmissionAnswer(value.TemplateItemId, value.OptionCode)).ToArray(),
            criteria,
            rules,
            responses.ToDictionary(value => value.TemplateItemId, value => value.Id));
    }

    private sealed record SubmissionInputs(
        int CompanyId,
        IReadOnlyCollection<EvaluationSubmissionQuestion> Questions,
        IReadOnlyCollection<EvaluationSubmissionAnswer> Answers,
        IReadOnlyCollection<EvaluationSubmissionCriterion> Criteria,
        IReadOnlyList<EvaluationQualificationRule> Rules,
        IReadOnlyDictionary<int, int> ResponseIdByQuestionId);
}
