using System.Security.Claims;
using EBR.Application.Evaluations;
using EBR.Domain.Evaluations;
using EBR.Domain.Identity;
using EBR.Domain.Workflow;
using EBR.Infrastructure.Persistence;
using EBR.Infrastructure.Risk;
using Microsoft.EntityFrameworkCore;

namespace EBR.Api.Endpoints;

/// <summary>
/// Ejecución de la evaluación BPM de un expediente (RF-12, RF-13): iniciar una instancia desde la
/// plantilla publicada vigente, capturar respuestas por pregunta con autosave idempotente, consultar
/// el progreso de captura y enviar (bloquear) la evaluación. A diferencia del resto de
/// <c>/api/cases</c>, que autoriza solo Administrador/Coordinador, estas rutas son exclusivas del
/// Técnico Evaluador: quien ejecuta la ficha en campo es el técnico, no quien coordina o administra.
/// </summary>
public static class EvaluationInstanceEndpoints
{
    public static IEndpointRouteBuilder MapEvaluationInstanceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/cases/{id:int}/evaluations", StartAsync)
            .WithTags("Evaluaciones")
            .RequireAuthorization(policy => policy.RequireRole(SystemRoles.Evaluator));

        var group = endpoints.MapGroup("/api/evaluations").WithTags("Evaluaciones")
            .RequireAuthorization(policy => policy.RequireRole(SystemRoles.Evaluator));
        group.MapGet("/{id:int}", GetAsync);
        group.MapPut("/{id:int}/responses/{itemId:int}", SaveResponseAsync);
        group.MapPost("/{id:int}/submit", SubmitAsync);

        endpoints.MapGet("/api/evaluations/{id:int}/result", GetResultAsync)
            .WithTags("Evaluaciones")
            .RequireAuthorization(policy => policy.RequireRole(
                SystemRoles.Evaluator, SystemRoles.Coordinator, SystemRoles.Administrator));
        return endpoints;
    }

    /// <summary>
    /// Inicia la instancia de evaluación de un caso (RF-12). Exige que el técnico que llama sea el
    /// asignado vigente del caso (<see cref="CaseAssignment"/>) y que el caso esté en un estado desde
    /// el que la máquina de estados permita transicionar a <see cref="CaseStatuses.InEvaluation"/>
    /// (en la práctica, <c>SCHEDULED</c>). <strong>Criterio de selección de plantilla</strong>: el SRS
    /// no define cómo elegir entre varias familias de plantillas publicadas simultáneamente y el
    /// modelo actual no asocia una empresa o un caso a una familia concreta, así que se usa la
    /// plantilla marcada explícitamente como activa (<see cref="ActiveEvaluationTemplate"/>,
    /// tabla <c>Plantilla_Activa</c>, administrada con
    /// <c>POST /api/evaluation-templates/{id}/activate</c>) en vez de "la publicada más
    /// recientemente": varias plantillas de prueba publicadas después de la oficial harían que ese
    /// criterio por fecha eligiera la equivocada. Documentado también en <c>DATABASE.md</c>. Solo puede
    /// existir una instancia por caso (índice único <c>caso_id</c> en <c>Evaluacion_Instancia</c>): un
    /// segundo intento responde <c>409</c>.
    /// </summary>
    private static async Task<IResult> StartAsync(
        int id,
        ClaimsPrincipal principal,
        EbrDbContext context,
        RiskRuleVersionProvisioner provisioner,
        CancellationToken cancellationToken)
    {
        var inspectionCase = await context.InspectionCases.SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (inspectionCase is null) return Results.NotFound();

        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var actingUserId)) return Results.Unauthorized();

        var assignment = await context.CaseAssignments.AsNoTracking()
            .SingleOrDefaultAsync(value => value.CaseId == id && value.IsCurrent, cancellationToken);
        if (assignment is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["technicianId"] = ["El caso debe tener un técnico asignado antes de iniciar la evaluación."]
            });
        }

        if (assignment.TechnicianId != actingUserId) return Results.Forbid();

        var existingInstance = await context.EvaluationInstances.AsNoTracking()
            .SingleOrDefaultAsync(value => value.CaseId == id, cancellationToken);
        if (existingInstance is not null) return Results.Ok(existingInstance);

        if (!CaseStateMachine.CanTransition(inspectionCase.Status, CaseStatuses.InEvaluation))
            return Results.Conflict(new { message = $"No se permite iniciar una evaluación desde el estado {inspectionCase.Status}." });

        var activeTemplateId = await context.ActiveEvaluationTemplates.AsNoTracking()
            .Where(value => value.Id == ActiveEvaluationTemplate.SingletonId)
            .Select(value => (int?)value.TemplateId)
            .SingleOrDefaultAsync(cancellationToken);
        if (activeTemplateId is null)
        {
            return Results.Conflict(new
            {
                message = "No hay una plantilla de evaluación activa. Un administrador debe activar una plantilla " +
                    "publicada en /api/evaluation-templates/{id}/activate antes de iniciar evaluaciones."
            });
        }

        var template = await context.EvaluationTemplates.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == activeTemplateId.Value, cancellationToken);
        if (template is null || template.Status != EvaluationTemplateStatuses.Published)
        {
            return Results.Conflict(new
            {
                message = "La plantilla activa ya no está publicada. Un administrador debe activar otra plantilla " +
                    "publicada antes de iniciar evaluaciones."
            });
        }

        // La instancia congela la versión de reglas de riesgo con la que se calificará al enviarla, para
        // que publicar otra versión durante la inspección no cambie el resultado de esta evaluación.
        var ruleVersion = await provisioner.FindPublishedAsync(cancellationToken);
        if (ruleVersion is null)
            return Results.Conflict(new { message = "No hay una versión publicada de reglas de riesgo con la que calificar la evaluación." });

        if (context.Database.IsNpgsql())
        {
            var problem = await PostgresCommandProblem.ExecuteAsync(() => context.Database.ExecuteSqlInterpolatedAsync(
                $"CALL sp_iniciar_evaluacion({id}, {actingUserId}, {ruleVersion.Id})", cancellationToken));
            if (problem is not null) return problem;
            context.ChangeTracker.Clear();
            var stored = await context.EvaluationInstances.AsNoTracking()
                .SingleAsync(value => value.CaseId == id, cancellationToken);
            return Results.Created($"/api/evaluations/{stored.Id}", stored);
        }

        var previousStatus = inspectionCase.Status;
        inspectionCase.Status = CaseStatuses.InEvaluation;
        context.CaseStateHistories.Add(new CaseStateHistory
        {
            CaseId = inspectionCase.Id,
            PreviousStatus = previousStatus,
            NewStatus = CaseStatuses.InEvaluation,
            Reason = "Inicio de evaluación",
            ChangedBy = actingUserId
        });

        var instance = new EvaluationInstance
        {
            CaseId = id,
            TemplateId = template.Id,
            TemplateFamilyId = template.FamilyId,
            RiskRuleVersionId = ruleVersion.Id,
            Status = EvaluationInstanceStatuses.InProgress,
            StartedBy = actingUserId
        };
        context.EvaluationInstances.Add(instance);
        await context.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/evaluations/{instance.Id}", instance);
    }

    /// <summary>
    /// Devuelve la instancia con su progreso de captura: cuántas preguntas activas tiene la plantilla
    /// congelada de la instancia y cuántas ya tienen una fila en <c>Evaluacion_Respuesta</c>.
    /// <strong>Distinto del porcentaje BPM</strong> (<see cref="BpmScoreCalculator"/>): el progreso de
    /// captura cuenta toda pregunta con una respuesta guardada, incluida <c>NA</c>, sobre el total de
    /// preguntas de la plantilla; el porcentaje BPM pondera por peso y excluye <c>NA</c> del
    /// denominador. Son dos métricas distintas que no deben confundirse ni sumarse.
    /// </summary>
    private static async Task<IResult> GetAsync(int id, ClaimsPrincipal principal, EbrDbContext context, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var actingUserId)) return Results.Unauthorized();
        var instance = await context.EvaluationInstances.AsNoTracking().SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (instance is null) return Results.NotFound();
        var assignment = await context.CaseAssignments.AsNoTracking()
            .SingleOrDefaultAsync(value => value.CaseId == instance.CaseId && value.IsCurrent, cancellationToken);
        if (assignment is null || assignment.TechnicianId != actingUserId) return Results.Forbid();

        var totalQuestions = await context.EvaluationTemplateItems.AsNoTracking()
            .CountAsync(value => value.TemplateId == instance.TemplateId && value.IsActive && value.ItemType == EvaluationItemTypes.Question, cancellationToken);
        var answeredQuestions = await context.EvaluationResponses.AsNoTracking()
            .CountAsync(value => value.EvaluationInstanceId == id, cancellationToken);
        var progressPercentage = totalQuestions == 0 ? (decimal?)null : Math.Round((decimal)answeredQuestions / totalQuestions * 100m, 2);

        return Results.Ok(new EvaluationInstanceProgressResponse(
            instance.Id,
            instance.CaseId,
            instance.TemplateId,
            instance.Status,
            instance.StartedAt,
            instance.StartedBy,
            instance.SubmittedAt,
            instance.SubmittedBy,
            totalQuestions,
            answeredQuestions,
            progressPercentage));
    }

    /// <summary>
    /// Guarda o actualiza la respuesta de una pregunta (autosave, RF-13). Idempotente: si ya existe una
    /// fila para esa instancia y pregunta, se actualiza en lugar de duplicarse — reforzado con el
    /// índice único real (<c>instancia_id</c>, <c>item_id</c>) en <c>Evaluacion_Respuesta</c>. Rechaza
    /// con <c>409</c> si la instancia ya está <c>SUBMITTED</c> (bloqueada) y con <c>403</c> si quien
    /// llama no es el técnico actualmente asignado al caso — se valida contra la asignación vigente y
    /// no contra el técnico que inició la instancia, para que una reasignación en curso (permitida por
    /// <see cref="CaseStateMachine.CanReassign"/> mientras el caso está en <c>IN_EVALUATION</c>) le dé
    /// al nuevo técnico la posibilidad de continuar capturando.
    /// </summary>
    private static async Task<IResult> SaveResponseAsync(
        int id,
        int itemId,
        SaveResponseRequest request,
        ClaimsPrincipal principal,
        EbrDbContext context,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var actingUserId)) return Results.Unauthorized();

        var instance = await context.EvaluationInstances.SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (instance is null) return Results.NotFound();

        if (instance.Status == EvaluationInstanceStatuses.Submitted)
            return Results.Conflict(new { message = "La evaluación ya fue enviada y sus respuestas están bloqueadas." });

        var assignment = await context.CaseAssignments.AsNoTracking()
            .SingleOrDefaultAsync(value => value.CaseId == instance.CaseId && value.IsCurrent, cancellationToken);
        if (assignment is null || assignment.TechnicianId != actingUserId) return Results.Forbid();

        var item = await context.EvaluationTemplateItems.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == itemId && value.TemplateId == instance.TemplateId && value.IsActive, cancellationToken);
        if (item is null || item.ItemType != EvaluationItemTypes.Question)
            return Validation("El ítem debe ser una pregunta activa de la plantilla de esta evaluación.");

        if (string.IsNullOrWhiteSpace(request.OptionCode)) return Validation("La opción de respuesta es obligatoria.");
        var optionCode = request.OptionCode.Trim().ToUpperInvariant();

        var option = await context.EvaluationResponseOptions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.TemplateId == instance.TemplateId && value.Code == optionCode, cancellationToken);
        if (option is null) return Validation("La opción de respuesta no pertenece a la plantilla de esta evaluación.");

        if (optionCode == BpmResponseOptions.NotApplicable && !item.AllowsNotApplicable)
            return Validation("Esta pregunta no admite la opción No Aplica.");

        var observations = request.Observations?.Trim() ?? "";
        var comments = request.Comments?.Trim() ?? "";

        if (context.Database.IsNpgsql())
        {
            var problem = await PostgresCommandProblem.ExecuteAsync(() => context.Database.ExecuteSqlInterpolatedAsync(
                $"CALL sp_guardar_respuestas({id}, {itemId}, {optionCode}, {observations}, {comments}, {actingUserId})",
                cancellationToken));
            if (problem is not null) return problem;
            context.ChangeTracker.Clear();
            return Results.Ok(await context.EvaluationResponses.AsNoTracking()
                .SingleAsync(value => value.EvaluationInstanceId == id && value.TemplateItemId == itemId, cancellationToken));
        }

        var response = await context.EvaluationResponses
            .SingleOrDefaultAsync(value => value.EvaluationInstanceId == id && value.TemplateItemId == itemId, cancellationToken);
        if (response is null)
        {
            response = new EvaluationResponse
            {
                EvaluationInstanceId = id,
                TemplateItemId = itemId,
                OptionCode = optionCode,
                Observations = observations,
                Comments = comments,
                SavedBy = actingUserId
            };
            context.EvaluationResponses.Add(response);
        }
        else
        {
            response.OptionCode = optionCode;
            response.Observations = observations;
            response.Comments = comments;
            response.SavedAt = DateTimeOffset.UtcNow;
            response.SavedBy = actingUserId;
        }

        await context.SaveChangesAsync(cancellationToken);
        return Results.Ok(response);
    }

    /// <summary>
    /// Envía (bloquea) la evaluación, calcula su resultado y transiciona el caso a
    /// <see cref="CaseStatuses.PendingReport"/>. <strong>Completitud</strong>: el envío exige que toda
    /// pregunta activa de la plantilla tenga respuesta, porque el porcentaje BPM se define sobre la
    /// ficha completa: una pregunta sin responder no es lo mismo que un <c>NA</c> (que sí se declara y
    /// sale del denominador), y admitirla haría que el porcentaje dependiera de cuánto se capturó y no
    /// de lo observado. La comprobación se hace antes de bloquear
    /// (<see cref="IEvaluationSubmissionService.DescribeSubmissionBlockerAsync"/>), de modo que una
    /// evaluación que no puede calificarse se queda intacta en <c>IN_PROGRESS</c> y el técnico puede
    /// completarla. El progreso de captura (<see cref="GetAsync"/>) le indica cuánto le falta.
    /// </summary>
    private static async Task<IResult> SubmitAsync(
        int id,
        ClaimsPrincipal principal,
        EbrDbContext context,
        IEvaluationSubmissionService submissionService,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var actingUserId)) return Results.Unauthorized();

        var instance = await context.EvaluationInstances.SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (instance is null) return Results.NotFound();
        if (instance.Status == EvaluationInstanceStatuses.Submitted)
            return Results.Conflict(new { message = "La evaluación ya fue enviada." });

        var assignment = await context.CaseAssignments.AsNoTracking()
            .SingleOrDefaultAsync(value => value.CaseId == instance.CaseId && value.IsCurrent, cancellationToken);
        if (assignment is null || assignment.TechnicianId != actingUserId) return Results.Forbid();

        var inspectionCase = await context.InspectionCases.SingleOrDefaultAsync(value => value.Id == instance.CaseId, cancellationToken);
        if (inspectionCase is null) return Results.NotFound();

        if (!CaseStateMachine.CanTransition(inspectionCase.Status, CaseStatuses.PendingReport))
            return Results.Conflict(new { message = $"No se permite enviar la evaluación desde el estado {inspectionCase.Status} del caso." });

        var blocker = await submissionService.DescribeSubmissionBlockerAsync(id, cancellationToken);
        if (blocker is not null) return Results.Conflict(new { message = blocker });

        if (context.Database.IsNpgsql())
        {
            var problem = await PostgresCommandProblem.ExecuteAsync(() => context.Database.ExecuteSqlInterpolatedAsync(
                $"CALL sp_enviar_evaluacion({id}, {actingUserId})", cancellationToken));
            if (problem is not null) return problem;
            context.ChangeTracker.Clear();
            await submissionService.RegisterResultAsync(id, actingUserId, cancellationToken);
            context.ChangeTracker.Clear();
            return Results.Ok(await context.EvaluationInstances.AsNoTracking()
                .SingleAsync(value => value.Id == id, cancellationToken));
        }

        instance.Status = EvaluationInstanceStatuses.Submitted;
        instance.SubmittedAt = DateTimeOffset.UtcNow;
        instance.SubmittedBy = actingUserId;

        var previousStatus = inspectionCase.Status;
        inspectionCase.Status = CaseStatuses.PendingReport;
        context.CaseStateHistories.Add(new CaseStateHistory
        {
            CaseId = inspectionCase.Id,
            PreviousStatus = previousStatus,
            NewStatus = CaseStatuses.PendingReport,
            Reason = "Envío de evaluación",
            ChangedBy = actingUserId
        });

        await context.SaveChangesAsync(cancellationToken);
        await submissionService.RegisterResultAsync(id, actingUserId, cancellationToken);
        return Results.Ok(instance);
    }

    /// <summary>
    /// Devuelve la fotografía inmutable del resultado de una evaluación enviada (RF-14): porcentaje BPM,
    /// calificación, no conformidades por severidad y el cálculo de riesgo con su frecuencia de
    /// inspección. Los valores se leen tal como quedaron guardados en el envío; publicar después otra
    /// versión de reglas de riesgo no los altera. Responde <c>404</c> mientras la evaluación siga en
    /// <c>IN_PROGRESS</c>, porque hasta el envío no existe resultado. Además del técnico asignado lo
    /// consultan coordinación y administración, que son quienes dan seguimiento al expediente.
    /// </summary>
    private static async Task<IResult> GetResultAsync(
        int id,
        ClaimsPrincipal principal,
        EbrDbContext context,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var actingUserId)) return Results.Unauthorized();

        var instance = await context.EvaluationInstances.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (instance is null) return Results.NotFound();

        if (principal.IsInRole(SystemRoles.Evaluator) &&
            !principal.IsInRole(SystemRoles.Coordinator) && !principal.IsInRole(SystemRoles.Administrator))
        {
            var assignment = await context.CaseAssignments.AsNoTracking()
                .SingleOrDefaultAsync(value => value.CaseId == instance.CaseId && value.IsCurrent, cancellationToken);
            if (assignment is null || assignment.TechnicianId != actingUserId) return Results.Forbid();
        }

        var result = await context.EvaluationResults.AsNoTracking()
            .SingleOrDefaultAsync(value => value.EvaluationInstanceId == id, cancellationToken);
        if (result is null) return Results.NotFound();

        var calculation = await context.RiskCalculations.AsNoTracking()
            .SingleAsync(value => value.Id == result.RiskCalculationId, cancellationToken);
        var riskLevel = await context.RiskLevels.AsNoTracking()
            .Where(value => value.Id == calculation.RiskLevelId)
            .Select(value => value.Name)
            .SingleAsync(cancellationToken);
        var nonConformities = await context.EvaluationNonConformities.AsNoTracking()
            .Where(value => value.EvaluationResultId == result.Id)
            .Join(context.EvaluationGuidanceCriteria.AsNoTracking(), value => value.GuidanceCriterionId, criterion => criterion.Id,
                (value, criterion) => new { value, criterion })
            .Join(context.EvaluationTemplateItems.AsNoTracking(), pair => pair.criterion.ItemId, item => item.Id,
                (pair, item) => new EvaluationNonConformityResponse(
                    pair.value.Id,
                    pair.value.EvaluationResponseId,
                    pair.value.GuidanceCriterionId,
                    item.Code,
                    pair.criterion.Code,
                    pair.value.Severity))
            .ToListAsync(cancellationToken);

        return Results.Ok(new EvaluationResultResponse(
            result.Id,
            result.EvaluationInstanceId,
            instance.CaseId,
            result.BpmPoints,
            result.BpmDenominator,
            result.BpmPercentage,
            result.QualificationCode,
            result.Classification,
            result.BpmRiskScore,
            result.CriticalCount,
            result.MajorCount,
            result.MinorCount,
            result.RiskCalculationId,
            calculation.ProductRisk,
            calculation.EstablishmentRisk,
            calculation.TotalRisk,
            riskLevel,
            result.FrequencyMonths,
            result.CalculatedAt,
            nonConformities));
    }

    private static IResult Validation(string message) => Results.ValidationProblem(new Dictionary<string, string[]> { ["response"] = [message] });

    private sealed record SaveResponseRequest(string OptionCode, string? Observations = null, string? Comments = null);

    private sealed record EvaluationResultResponse(
        int EvaluationResultId,
        int EvaluationInstanceId,
        int CaseId,
        decimal BpmPoints,
        decimal BpmDenominator,
        decimal BpmPercentage,
        string QualificationCode,
        string Classification,
        decimal BpmRiskScore,
        int CriticalCount,
        int MajorCount,
        int MinorCount,
        int RiskCalculationId,
        decimal ProductRisk,
        decimal EstablishmentRisk,
        decimal TotalRisk,
        string RiskLevel,
        int FrequencyMonths,
        DateTimeOffset CalculatedAt,
        IReadOnlyList<EvaluationNonConformityResponse> NonConformities);

    private sealed record EvaluationNonConformityResponse(
        int Id,
        int EvaluationResponseId,
        int GuidanceCriterionId,
        string ItemCode,
        string CriterionCode,
        string Severity);

    private sealed record EvaluationInstanceProgressResponse(
        int Id,
        int CaseId,
        int TemplateId,
        string Status,
        DateTimeOffset StartedAt,
        Guid StartedBy,
        DateTimeOffset? SubmittedAt,
        Guid? SubmittedBy,
        int TotalQuestions,
        int AnsweredQuestions,
        decimal? ProgressPercentage);
}
