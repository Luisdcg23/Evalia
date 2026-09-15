namespace EBR.Domain.Evaluations;

/// <summary>
/// Resuelve la banda de calificación aplicable a un porcentaje BPM contra las reglas
/// (<see cref="EvaluationQualificationRule"/>) de la plantilla congelada en la evaluación. Las bandas
/// no se codifican aquí: provienen de la ficha oficial importada, así que dos plantillas con bandas
/// distintas califican distinto sin tocar código.
/// </summary>
public static class BpmQualificationSelector
{
    /// <summary>
    /// Devuelve la única banda cuyo intervalo contiene el porcentaje. Devuelve <c>null</c> si la
    /// plantilla no declara bandas o si ninguna cubre el porcentaje: el llamador debe rechazar el
    /// envío en lugar de inventar una clasificación.
    /// </summary>
    public static EvaluationQualificationRule? Select(
        IReadOnlyCollection<EvaluationQualificationRule> rules,
        decimal percentage)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var matches = rules.Where(rule => Contains(rule, percentage)).ToArray();
        return matches.Length == 1
            ? matches[0]
            : matches.Length == 0
                ? null
                : throw new InvalidOperationException(
                    $"El porcentaje {percentage} cae en {matches.Length} bandas de calificación solapadas " +
                    $"({string.Join(", ", matches.Select(rule => rule.Code))}).");
    }

    private static bool Contains(EvaluationQualificationRule rule, decimal percentage)
    {
        if (rule.MinPercentage is { } minimum &&
            (rule.MinIncluded ? percentage < minimum : percentage <= minimum)) return false;
        if (rule.MaxPercentage is { } maximum &&
            (rule.MaxIncluded ? percentage > maximum : percentage >= maximum)) return false;
        return true;
    }
}

public sealed record EvaluationSubmissionQuestion(int Id, string Code, decimal Weight, bool AllowsNotApplicable);
public sealed record EvaluationSubmissionAnswer(int QuestionId, string OptionCode);
public sealed record EvaluationSubmissionCriterion(int Id, int QuestionId, string Code, string? Severity);
public sealed record EvaluationSubmissionNonConformity(int CriterionId, int QuestionId, string Severity);

public sealed record EvaluationSubmissionResult(
    BpmScore Score,
    IReadOnlyList<EvaluationSubmissionNonConformity> NonConformities,
    int CriticalCount,
    int MajorCount,
    int MinorCount);

/// <summary>
/// Consolida el resultado de una evaluación enviada: porcentaje BPM ponderado y no conformidades
/// derivadas de los criterios de la guía de llenado asociados a las preguntas incumplidas.
/// </summary>
public static class EvaluationSubmissionCalculator
{
    /// <summary>
    /// <strong>Exige respuesta para todas las preguntas activas</strong>: el porcentaje BPM se define
    /// sobre la ficha completa, así que calcularlo con preguntas sin capturar produciría un
    /// cumplimiento sobre un denominador parcial que no es el del establecimiento. Las preguntas
    /// marcadas <c>NA</c> sí quedan fuera del denominador, que es la vía prevista por la ficha para
    /// las secciones que no aplican.
    /// </summary>
    public static EvaluationSubmissionResult Calculate(
        IReadOnlyCollection<EvaluationSubmissionQuestion> questions,
        IReadOnlyCollection<EvaluationSubmissionAnswer> answers,
        IReadOnlyCollection<EvaluationSubmissionCriterion>? criteria = null)
    {
        ArgumentNullException.ThrowIfNull(questions);
        ArgumentNullException.ThrowIfNull(answers);
        criteria ??= [];

        var answerByQuestion = answers.ToDictionary(answer => answer.QuestionId);
        var missing = questions.Where(question => !answerByQuestion.ContainsKey(question.Id))
            .Select(question => question.Code).ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException($"Faltan respuestas para las preguntas: {string.Join(", ", missing)}.");

        foreach (var question in questions)
        {
            var answer = answerByQuestion[question.Id];
            if (answer.OptionCode == BpmResponseOptions.NotApplicable && !question.AllowsNotApplicable)
                throw new InvalidOperationException($"La pregunta {question.Code} no admite No Aplica.");
        }

        var score = BpmScoreCalculator.Calculate(questions.Select(question => new BpmAnswer(
            question.Code, answerByQuestion[question.Id].OptionCode, question.Weight)).ToArray());
        if (score.Percentage is null)
            throw new InvalidOperationException("La evaluación no contiene preguntas aplicables y no puede calificarse.");

        var affectedQuestionIds = answers
            .Where(answer => answer.OptionCode is BpmResponseOptions.PartialCompliance or BpmResponseOptions.TotalNonCompliance)
            .Select(answer => answer.QuestionId).ToHashSet();
        var nonConformities = criteria
            .Where(criterion => affectedQuestionIds.Contains(criterion.QuestionId) && criterion.Severity is not null)
            .Select(criterion => new EvaluationSubmissionNonConformity(
                criterion.Id, criterion.QuestionId, criterion.Severity!)).ToArray();

        return new EvaluationSubmissionResult(
            score,
            nonConformities,
            nonConformities.Count(item => item.Severity == EvaluationCriticalityLevels.Critical),
            nonConformities.Count(item => item.Severity == EvaluationCriticalityLevels.Major),
            nonConformities.Count(item => item.Severity == EvaluationCriticalityLevels.Minor));
    }
}
