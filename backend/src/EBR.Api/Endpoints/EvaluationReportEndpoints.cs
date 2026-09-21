using System.Security.Claims;
using System.Security.Cryptography;
using EBR.Application.Email;
using EBR.Application.Evidence;
using EBR.Application.Reports;
using EBR.Domain.Evaluations;
using EBR.Domain.Identity;
using EBR.Domain.Workflow;
using EBR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EBR.Api.Endpoints;

/// <summary>
/// Informe de evaluación versionado (fase 5.1, RF-16). Cada emisión crea una versión nueva y las
/// anteriores se conservan.
/// </summary>
public static class EvaluationReportEndpoints
{
    /// <summary>Tope de la rúbrica escrita, igual al de la columna <c>firma_nombre</c>.</summary>
    private const int SignatureNameMaxLength = 80;

    public static IEndpointRouteBuilder MapEvaluationReportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/evaluations/{id:int}/report", IssueAsync)
            .WithTags("Evaluaciones")
            .RequireAuthorization(policy => policy.RequireRole(SystemRoles.Evaluator));

        endpoints.MapGet("/api/evaluations/{id:int}/report/versions", ListVersionsAsync)
            .WithTags("Evaluaciones")
            .RequireAuthorization(policy => policy.RequireRole(
                SystemRoles.Evaluator, SystemRoles.Coordinator, SystemRoles.Administrator));

        endpoints.MapPost("/api/evaluations/{id:int}/report/review", ReviewAsync)
            .WithTags("Evaluaciones")
            .RequireAuthorization(policy => policy.RequireRole(SystemRoles.Coordinator));

        endpoints.MapGet("/api/evaluations/{id:int}/report/reviews", ListReviewsAsync)
            .WithTags("Evaluaciones")
            .RequireAuthorization(policy => policy.RequireRole(
                SystemRoles.Evaluator, SystemRoles.Coordinator, SystemRoles.Administrator));

        endpoints.MapGet("/api/evaluations/{id:int}/report", GetCurrentAsync)
            .WithTags("Evaluaciones")
            .RequireAuthorization(policy => policy.RequireRole(
                SystemRoles.Evaluator, SystemRoles.Coordinator, SystemRoles.Administrator));

        endpoints.MapPost("/api/evaluations/{id:int}/report/official", GenerateOfficialAsync)
            .WithTags("Evaluaciones")
            .RequireAuthorization(policy => policy.RequireRole(SystemRoles.Coordinator));

        endpoints.MapGet("/api/evaluations/{id:int}/report/official/content", DownloadOfficialAsync)
            .WithTags("Evaluaciones")
            .RequireAuthorization(policy => policy.RequireRole(
                SystemRoles.Evaluator, SystemRoles.Coordinator, SystemRoles.Administrator));

        endpoints.MapGet("/api/evaluations/{id:int}/report/official/verify", VerifyOfficialAsync)
            .WithTags("Evaluaciones")
            .RequireAuthorization(policy => policy.RequireRole(
                SystemRoles.Evaluator, SystemRoles.Coordinator, SystemRoles.Administrator));

        endpoints.MapGet("/api/evaluations/{id:int}/report/preview", PreviewAsync)
            .WithTags("Evaluaciones")
            .RequireAuthorization(policy => policy.RequireRole(SystemRoles.Coordinator));

        endpoints.MapPost("/api/cases/{id:int}/close", CloseCaseAsync)
            .WithTags("Casos")
            .RequireAuthorization(policy => policy.RequireRole(SystemRoles.Coordinator));

        return endpoints;
    }

    /// <summary>Historial completo del informe, de la versión más antigua a la más reciente.</summary>
    private static async Task<IResult> ListVersionsAsync(
        int id,
        ClaimsPrincipal principal,
        EbrDbContext context,
        CancellationToken cancellationToken)
    {
        var instance = await context.EvaluationInstances.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (instance is null) return Results.NotFound();
        if (!await CanReadAsync(principal, instance.CaseId, context, cancellationToken)) return Results.Forbid();

        var reports = await context.EvaluationReports.AsNoTracking()
            .Where(value => value.EvaluationInstanceId == id)
            .OrderBy(value => value.Version)
            .ToListAsync(cancellationToken);

        return Results.Ok(reports.Select(Describe));
    }

    /// <summary>Versión vigente del informe, que es siempre la última emitida.</summary>
    private static async Task<IResult> GetCurrentAsync(
        int id,
        ClaimsPrincipal principal,
        EbrDbContext context,
        CancellationToken cancellationToken)
    {
        var instance = await context.EvaluationInstances.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (instance is null) return Results.NotFound();
        if (!await CanReadAsync(principal, instance.CaseId, context, cancellationToken)) return Results.Forbid();

        var report = await context.EvaluationReports.AsNoTracking()
            .Where(value => value.EvaluationInstanceId == id)
            .OrderByDescending(value => value.Version)
            .FirstOrDefaultAsync(cancellationToken);

        return report is null ? Results.NotFound() : Results.Ok(Describe(report));
    }

    /// <summary>
    /// El coordinador y la administración supervisan todos los expedientes; el técnico solo lee el
    /// informe del expediente que tiene asignado.
    /// </summary>
    private static async Task<bool> CanReadAsync(
        ClaimsPrincipal principal,
        int caseId,
        EbrDbContext context,
        CancellationToken cancellationToken)
    {
        if (principal.IsInRole(SystemRoles.Coordinator) || principal.IsInRole(SystemRoles.Administrator))
            return true;

        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var actingUserId))
            return false;

        return await context.CaseAssignments.AsNoTracking()
            .AnyAsync(value => value.CaseId == caseId && value.TechnicianId == actingUserId, cancellationToken);
    }

    private static async Task<IResult> IssueAsync(
        int id,
        IssueReportRequest request,
        ClaimsPrincipal principal,
        EbrDbContext context,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var actingUserId))
            return Results.Unauthorized();

        // Emitir el informe es firmarlo: la rúbrica que escribe el técnico es lo que se estampa en el
        // PDF oficial, así que un informe emitido sin firma no es un estado que el expediente admita.
        var signatureName = (request.SignatureName ?? string.Empty).Trim();
        if (signatureName.Length == 0)
            return Results.BadRequest(new { message = "Emitir el informe exige la firma del técnico." });
        if (signatureName.Length > SignatureNameMaxLength)
            return Results.BadRequest(new { message = $"La firma no admite más de {SignatureNameMaxLength} caracteres." });

        var instance = await context.EvaluationInstances.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (instance is null) return Results.NotFound();

        // El informe describe una evaluación cerrada. Emitirlo con la captura abierta produciría un
        // documento sobre datos que todavía pueden cambiar.
        if (instance.Status != EvaluationInstanceStatuses.Submitted)
            return Results.Conflict(new { message = "La evaluación todavía no ha sido enviada y no admite informe." });

        // El informe lo emite quien tiene la asignación vigente del expediente, no quien inició la
        // evaluación: una reasignación en curso traslada también la responsabilidad del informe.
        var assignment = await context.CaseAssignments.AsNoTracking()
            .SingleOrDefaultAsync(value => value.CaseId == instance.CaseId && value.IsCurrent, cancellationToken);
        if (assignment is null || assignment.TechnicianId != actingUserId) return Results.Forbid();

        var inspectionCase = await context.InspectionCases
            .SingleOrDefaultAsync(value => value.Id == instance.CaseId, cancellationToken);
        if (inspectionCase is null) return Results.NotFound();

        // Emitir el informe es lo que lo pone en manos del coordinador: el expediente pasa a revisión,
        // tanto la primera vez como al reenviarlo después de una corrección. Un expediente que ya está
        // en revisión también admite versión nueva —el técnico puede corregirse antes de que el
        // coordinador se pronuncie—, y en ese caso no cambia de estado. Aprobado o cerrado ya no: el
        // informe aprobado es lo que se comunicó al establecimiento.
        var alreadyUnderReview = inspectionCase.Status == CaseStatuses.InReview;
        if (!alreadyUnderReview && !CaseStateMachine.CanTransition(inspectionCase.Status, CaseStatuses.InReview))
            return Results.Conflict(new { message = $"El expediente en estado {inspectionCase.Status} no admite una versión nueva del informe." });

        var lastVersion = await context.EvaluationReports.AsNoTracking()
            .Where(value => value.EvaluationInstanceId == id)
            .MaxAsync(value => (int?)value.Version, cancellationToken) ?? 0;

        var report = new EvaluationReport
        {
            EvaluationInstanceId = id,
            Version = lastVersion + 1,
            ExecutiveSummary = request.ExecutiveSummary.Trim(),
            Findings = request.Findings.Trim(),
            Recommendations = request.Recommendations.Trim(),
            SignatureName = signatureName,
            CreatedBy = actingUserId
        };
        context.EvaluationReports.Add(report);

        if (!alreadyUnderReview)
        {
            var previousStatus = inspectionCase.Status;
            inspectionCase.Status = CaseStatuses.InReview;
            context.CaseStateHistories.Add(new CaseStateHistory
            {
                CaseId = inspectionCase.Id,
                PreviousStatus = previousStatus,
                NewStatus = CaseStatuses.InReview,
                Reason = $"Emisión del informe versión {report.Version}",
                ChangedBy = actingUserId
            });
        }

        await context.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/evaluations/{id}/report/{report.Version}", Describe(report));
    }

    /// <summary>
    /// Revisión del coordinador sobre la versión vigente del informe (RF-17). La decisión queda como
    /// estado del informe y mueve el expediente: aprobar lo deja listo para el cierre.
    /// </summary>
    private static async Task<IResult> ReviewAsync(
        int id,
        ReviewReportRequest request,
        ClaimsPrincipal principal,
        EbrDbContext context,
        IEmailSender emailSender,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var actingUserId))
            return Results.Unauthorized();

        var instance = await context.EvaluationInstances.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (instance is null) return Results.NotFound();

        var report = await context.EvaluationReports
            .Where(value => value.EvaluationInstanceId == id)
            .OrderByDescending(value => value.Version)
            .FirstOrDefaultAsync(cancellationToken);
        if (report is null) return Results.NotFound();

        var inspectionCase = await context.InspectionCases
            .SingleOrDefaultAsync(value => value.Id == instance.CaseId, cancellationToken);
        if (inspectionCase is null) return Results.NotFound();

        // Solo se revisa lo que está en revisión: un informe ya aprobado no vuelve a decidirse, porque
        // eso cambiaría a posteriori lo que se comunicó al establecimiento.
        if (inspectionCase.Status != CaseStatuses.InReview)
            return Results.Conflict(new { message = $"El expediente en estado {inspectionCase.Status} no está en revisión." });

        var decision = (request.Decision ?? string.Empty).Trim().ToUpperInvariant();
        var observations = (request.Observations ?? string.Empty).Trim();
        var approves = decision == EvaluationReportDecisions.Approved;
        var signatureName = (request.SignatureName ?? string.Empty).Trim();

        // El estado del informe es la decisión, así que una decisión inventada lo dejaría en un estado
        // que no existe en el catálogo.
        if (!EvaluationReportDecisions.All.Contains(decision))
            return Results.BadRequest(new { message = $"La decisión {decision} no pertenece a la revisión del informe." });

        // Devolver el informe sin decir qué corregir deja al técnico sin nada que hacer (RF-18): las
        // observaciones son el contenido de la devolución, no un comentario opcional.
        if (!approves && observations.Length == 0)
            return Results.BadRequest(new { message = "Devolver el informe exige observaciones que indiquen qué corregir." });

        // Aprobar es lo que estampa la rúbrica del coordinador en el PDF oficial, así que solo esa
        // decisión se firma: devolver o pedir corrección no imprime nada y no admite firma.
        if (approves && signatureName.Length == 0)
            return Results.BadRequest(new { message = "Aprobar el informe exige la firma del coordinador." });
        if (signatureName.Length > SignatureNameMaxLength)
            return Results.BadRequest(new { message = $"La firma no admite más de {SignatureNameMaxLength} caracteres." });

        var signature = approves ? signatureName : null;

        if (context.Database.IsNpgsql())
        {
            var problem = await PostgresCommandProblem.ExecuteAsync(() => context.Database.ExecuteSqlInterpolatedAsync(
                $"CALL sp_revisar_informe({id}, {decision}, {observations}, {actingUserId}, {signature})", cancellationToken));
            if (problem is not null) return problem;
            context.ChangeTracker.Clear();
            report = await context.EvaluationReports.AsNoTracking()
                .Where(value => value.EvaluationInstanceId == id)
                .OrderByDescending(value => value.Version)
                .FirstAsync(cancellationToken);
        }
        else
        {
            report.Status = decision;
            var targetStatus = approves ? CaseStatuses.Approved : CaseStatuses.CorrectionRequired;
            inspectionCase.Status = targetStatus;
            context.EvaluationReportReviews.Add(new EvaluationReportReview
            {
                ReportId = report.Id,
                Decision = decision,
                Observations = observations,
                SignatureName = signature,
                ReviewedBy = actingUserId
            });
            context.CaseStateHistories.Add(new CaseStateHistory
            {
                CaseId = inspectionCase.Id,
                PreviousStatus = CaseStatuses.InReview,
                NewStatus = targetStatus,
                Reason = $"Revisión del informe versión {report.Version}: {decision}",
                ChangedBy = actingUserId
            });
            await context.SaveChangesAsync(cancellationToken);
        }

        var review = await context.EvaluationReportReviews.AsNoTracking()
            .Where(value => value.ReportId == report.Id)
            .OrderByDescending(value => value.Id)
            .FirstAsync(cancellationToken);

        var recipients = approves
            ? await context.CompanyUsers.AsNoTracking().Where(x => x.CompanyId == inspectionCase.CompanyId)
                .Select(x => x.UserId).ToListAsync(cancellationToken)
            : await context.CaseAssignments.AsNoTracking().Where(x => x.CaseId == inspectionCase.Id && x.IsCurrent)
                .Select(x => x.TechnicianId).ToListAsync(cancellationToken);
        await AddNotificationsAsync(context, emailSender, logger, recipients,
            approves ? NotificationTypes.ReportApproved : NotificationTypes.ReportCorrectionRequested,
            approves ? "Informe aprobado" : "Corrección de informe solicitada",
            approves ? $"El informe del expediente {inspectionCase.Id} fue aprobado."
                     : $"El informe del expediente {inspectionCase.Id} requiere correcciones: {observations}",
            inspectionCase.Id, $"report-review:{review.Id}", cancellationToken);

        return Results.Ok(Describe(review, report.Version));
    }

    private static async Task<IResult> GenerateOfficialAsync(
        int id, ClaimsPrincipal principal, EbrDbContext context, IEvidenceStorage storage,
        IOfficialReportRenderer renderer, IDocumentSigner documentSigner, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var actingUserId))
            return Results.Unauthorized();

        var report = await context.EvaluationReports.AsNoTracking()
            .Where(value => value.EvaluationInstanceId == id)
            .OrderByDescending(value => value.Version)
            .FirstOrDefaultAsync(cancellationToken);
        if (report is null) return Results.NotFound();
        if (report.Status != EvaluationReportDecisions.Approved)
            return Results.Conflict(new { message = "La última versión del informe debe estar aprobada antes de generar el PDF oficial." });

        var existing = await context.EvaluationOfficialReports.AsNoTracking()
            .SingleOrDefaultAsync(value => value.ReportId == report.Id, cancellationToken);
        if (existing is not null) return Results.Ok(DescribeOfficial(existing, id, report.Version));

        var reportContent = await BuildContentAsync(context, id, report, cancellationToken);
        if (reportContent is null)
            return Results.Conflict(new { message = "La evaluación no tiene un resultado calculado y no admite informe oficial." });

        var rendered = renderer.Render(reportContent);
        var bytes = rendered.Content;
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var fileName = $"informe-evaluacion-{id}-v{report.Version}.pdf";
        var storageKey = $"informes/evaluacion-{id}/{hash}.pdf";
        await using (var content = new MemoryStream(bytes, writable: false))
            await storage.SaveAsync(storageKey, "application/pdf", content, cancellationToken);

        // Firma electrónica (RF-19): se firma el binario exacto que se guardó, con la clave privada del
        // sistema; la huella de la clave pública queda en el metadato para poder verificar después
        // aunque la clave activa cambie con el tiempo.
        var signature = documentSigner.Sign(bytes);

        var official = new EvaluationOfficialReport
        {
            ReportId = report.Id,
            FileName = fileName,
            SizeBytes = bytes.LongLength,
            Sha256 = hash,
            StorageKey = storageKey,
            SignatureAlgorithm = documentSigner.Algorithm,
            SignatureBase64 = Convert.ToBase64String(signature),
            PublicKeyThumbprint = documentSigner.PublicKeyThumbprint,
            GeneratedBy = actingUserId
        };
        context.EvaluationOfficialReports.Add(official);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // La clave única por versión resuelve dos solicitudes simultáneas. Ambas generan los
            // mismos bytes y la misma clave por hash; la perdedora devuelve el metadato confirmado.
            context.ChangeTracker.Clear();
            official = await context.EvaluationOfficialReports.AsNoTracking()
                .SingleAsync(value => value.ReportId == report.Id, cancellationToken);
            return Results.Ok(DescribeOfficial(official, id, report.Version));
        }
        return Results.Created($"/api/evaluations/{id}/report/official/content", DescribeOfficial(official, id, report.Version));
    }

    private static async Task<IResult> DownloadOfficialAsync(
        int id, ClaimsPrincipal principal, EbrDbContext context, IEvidenceStorage storage,
        CancellationToken cancellationToken)
    {
        var instance = await context.EvaluationInstances.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (instance is null) return Results.NotFound();
        if (!await CanReadAsync(principal, instance.CaseId, context, cancellationToken)) return Results.Forbid();

        var item = await (from official in context.EvaluationOfficialReports.AsNoTracking()
                          join report in context.EvaluationReports.AsNoTracking() on official.ReportId equals report.Id
                          where report.EvaluationInstanceId == id
                          orderby report.Version descending
                          select official).FirstOrDefaultAsync(cancellationToken);
        if (item is null) return Results.NotFound();
        var content = await storage.OpenAsync(item.StorageKey, cancellationToken);
        return content is null ? Results.NotFound() : Results.File(content, item.MimeType, item.FileName);
    }

    /// <summary>
    /// Verifica el PDF oficial contra su firma electrónica (RF-19): recalcula el hash del binario tal
    /// como está hoy en el almacenamiento y comprueba la firma con la clave pública del sistema. Ambas
    /// comprobaciones son independientes —el hash detecta cualquier alteración del binario, la firma
    /// además certifica que lo emitió el sistema con la clave vigente al momento de generarlo— y las
    /// dos deben cumplirse para que el documento se considere válido.
    /// </summary>
    private static async Task<IResult> VerifyOfficialAsync(
        int id, ClaimsPrincipal principal, EbrDbContext context, IEvidenceStorage storage,
        IDocumentSigner documentSigner, CancellationToken cancellationToken)
    {
        var instance = await context.EvaluationInstances.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (instance is null) return Results.NotFound();
        if (!await CanReadAsync(principal, instance.CaseId, context, cancellationToken)) return Results.Forbid();

        var item = await (from official in context.EvaluationOfficialReports.AsNoTracking()
                          join report in context.EvaluationReports.AsNoTracking() on official.ReportId equals report.Id
                          where report.EvaluationInstanceId == id
                          orderby report.Version descending
                          select official).FirstOrDefaultAsync(cancellationToken);
        if (item is null) return Results.NotFound();

        var content = await storage.OpenAsync(item.StorageKey, cancellationToken);
        if (content is null) return Results.NotFound();

        byte[] bytes;
        await using (content)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            bytes = buffer.ToArray();
        }

        var hashMatches = string.Equals(
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), item.Sha256, StringComparison.Ordinal);
        var signatureValid = documentSigner.Verify(bytes, Convert.FromBase64String(item.SignatureBase64));

        return Results.Ok(new SignatureVerificationResponse(
            item.Id, id, hashMatches, signatureValid, hashMatches && signatureValid,
            item.SignatureAlgorithm, item.PublicKeyThumbprint, item.GeneratedAt, item.GeneratedBy));
    }

    /// <summary>
    /// Vista previa del informe para el coordinador, antes de que decida: el mismo PDF, con marca de agua
    /// "NO OFICIAL" y el estado real de la versión vigente. No se guarda ni se firma.
    /// </summary>
    private static async Task<IResult> PreviewAsync(
        int id, EbrDbContext context, IOfficialReportRenderer renderer, CancellationToken cancellationToken)
    {
        var report = await context.EvaluationReports.AsNoTracking()
            .Where(value => value.EvaluationInstanceId == id)
            .OrderByDescending(value => value.Version)
            .FirstOrDefaultAsync(cancellationToken);
        if (report is null) return Results.NotFound();

        var reportContent = await BuildContentAsync(context, id, report, cancellationToken);
        if (reportContent is null)
            return Results.Conflict(new { message = "La evaluación no tiene un resultado calculado y no admite vista previa." });

        var observations = await context.EvaluationReportReviews.AsNoTracking()
            .Where(value => value.ReportId == report.Id)
            .OrderByDescending(value => value.ReviewedAt)
            .Select(value => value.Observations)
            .FirstOrDefaultAsync(cancellationToken);

        var approved = report.Status == EvaluationReportDecisions.Approved;
        var preview = reportContent with
        {
            IsPreview = true,
            ReviewStatusLabel = DescribeReviewStatus(report.Status),
            ReviewObservations = approved || string.IsNullOrWhiteSpace(observations) ? null : observations
        };

        // Sin nombre de archivo, el navegador lo muestra en una pestaña en vez de descargarlo.
        return Results.File(renderer.Render(preview).Content, "application/pdf");
    }

    private static string DescribeReviewStatus(string reportStatus)
    {
        if (reportStatus == EvaluationReportDecisions.Approved) return OfficialReportStatusLabels.Approved;
        var normalized = reportStatus.ToUpperInvariant();
        if (normalized.Contains("CORRECTION", StringComparison.Ordinal)) return OfficialReportStatusLabels.CorrectionRequested;
        if (normalized.Contains("RETURN", StringComparison.Ordinal)) return OfficialReportStatusLabels.Returned;
        return OfficialReportStatusLabels.Pending;
    }

    private static async Task<IResult> CloseCaseAsync(
        int id, CloseCaseRequest request, ClaimsPrincipal principal, EbrDbContext context,
        IEmailSender emailSender, ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var actingUserId))
            return Results.Unauthorized();
        var result = request.Result?.Trim() ?? string.Empty;
        if (result.Length == 0) return Results.BadRequest(new { message = "El resultado del cierre es obligatorio." });

        var existing = await context.CaseClosures.AsNoTracking().SingleOrDefaultAsync(value => value.CaseId == id, cancellationToken);
        if (existing is not null) return Results.Ok(DescribeClosure(existing));

        var inspectionCase = await context.InspectionCases.SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (inspectionCase is null) return Results.NotFound();
        var report = await (from candidate in context.EvaluationReports
                            join instance in context.EvaluationInstances on candidate.EvaluationInstanceId equals instance.Id
                            where instance.CaseId == id
                            orderby candidate.Version descending
                            select candidate).FirstOrDefaultAsync(cancellationToken);
        if (report is null || report.Status != EvaluationReportDecisions.Approved || inspectionCase.Status != CaseStatuses.Approved)
            return Results.Conflict(new { message = "El expediente exige su última versión aprobada antes del cierre." });
        var official = await context.EvaluationOfficialReports.AsNoTracking()
            .SingleOrDefaultAsync(value => value.ReportId == report.Id, cancellationToken);
        if (official is null)
            return Results.Conflict(new { message = "El expediente exige el PDF oficial de la última versión aprobada." });

        if (context.Database.IsNpgsql())
        {
            var problem = await PostgresCommandProblem.ExecuteAsync(() => context.Database.ExecuteSqlInterpolatedAsync(
                $"CALL sp_cerrar_expediente({id}, {result}, {actingUserId})", cancellationToken));
            if (problem is not null) return problem;
            context.ChangeTracker.Clear();
        }
        else
        {
            inspectionCase.Status = CaseStatuses.Closed;
            context.CaseClosures.Add(new CaseClosure
            {
                CaseId = id, ReportId = report.Id, OfficialReportId = official.Id,
                Result = result, ClosedBy = actingUserId
            });
            context.CaseStateHistories.Add(new CaseStateHistory
            {
                CaseId = id, PreviousStatus = CaseStatuses.Approved, NewStatus = CaseStatuses.Closed,
                Reason = result, ChangedBy = actingUserId
            });
            await context.SaveChangesAsync(cancellationToken);
        }

        var closure = await context.CaseClosures.AsNoTracking().SingleAsync(value => value.CaseId == id, cancellationToken);
        var recipients = await context.CompanyUsers.AsNoTracking().Where(x => x.CompanyId == inspectionCase.CompanyId)
            .Select(x => x.UserId).ToListAsync(cancellationToken);
        await AddNotificationsAsync(context, emailSender, logger, recipients, NotificationTypes.CaseClosed,
            "Expediente cerrado", $"El expediente {id} fue cerrado oficialmente.", id,
            $"case-closure:{closure.Id}", cancellationToken);
        return Results.Ok(DescribeClosure(closure));
    }

    private static async Task AddNotificationsAsync(
        EbrDbContext context, IEmailSender emailSender, ILogger<Program> logger, IEnumerable<Guid> recipients,
        string type, string title, string message, int caseId, string operationPrefix,
        CancellationToken cancellationToken)
    {
        var newRecipients = new List<Guid>();
        foreach (var recipient in recipients.Distinct())
        {
            var operationId = $"{operationPrefix}:{recipient}";
            if (await context.Notifications.AnyAsync(x => x.OperationId == operationId, cancellationToken)) continue;
            context.Notifications.Add(new Notification
            {
                RecipientId = recipient,
                Type = type,
                Title = title,
                Message = message,
                ReferenceType = "CASE",
                ReferenceId = caseId,
                OperationId = operationId
            });
            newRecipients.Add(recipient);
        }
        await context.SaveChangesAsync(cancellationToken);

        // El correo es un complemento de la notificación in-app: solo se envía a quien recibió una
        // notificación nueva, y un proveedor SMTP caído no puede bloquear la revisión ni el cierre.
        foreach (var recipient in newRecipients)
        {
            await CaseEndpoints.SendNotificationEmailAsync(context, emailSender, logger, recipient, title, message, cancellationToken);
        }
    }

    /// <summary>
    /// Reúne los datos ya persistidos e inmutables que componen el PDF oficial: el texto del informe,
    /// la fotografía del resultado BPM y de riesgo, las no conformidades con su criterio de guía y las
    /// referencias de las evidencias de campo. Devuelve <c>null</c> si la evaluación no tiene resultado
    /// calculado.
    /// </summary>
    private static async Task<OfficialReportContent?> BuildContentAsync(
        EbrDbContext context, int instanceId, EvaluationReport report, CancellationToken cancellationToken)
    {
        var instance = await context.EvaluationInstances.AsNoTracking()
            .SingleAsync(value => value.Id == instanceId, cancellationToken);
        var inspectionCase = await context.InspectionCases.AsNoTracking()
            .SingleAsync(value => value.Id == instance.CaseId, cancellationToken);
        var company = await context.Companies.AsNoTracking()
            .SingleAsync(value => value.Id == inspectionCase.CompanyId, cancellationToken);

        var result = await context.EvaluationResults.AsNoTracking()
            .SingleOrDefaultAsync(value => value.EvaluationInstanceId == instanceId, cancellationToken);
        if (result is null) return null;

        var nonConformities = await (
            from nc in context.EvaluationNonConformities.AsNoTracking()
            join criterion in context.EvaluationGuidanceCriteria.AsNoTracking() on nc.GuidanceCriterionId equals criterion.Id
            where nc.EvaluationResultId == result.Id
            orderby criterion.Code
            select new OfficialReportNonConformity(nc.Severity, criterion.Code, criterion.Description))
            .ToListAsync(cancellationToken);

        var evidences = await context.EvaluationEvidences.AsNoTracking()
            .Where(value => value.EvaluationInstanceId == instanceId)
            .OrderBy(value => value.FileName)
            .Select(value => new OfficialReportEvidence(value.FileName, value.Hash, value.SizeBytes))
            .ToListAsync(cancellationToken);

        // Firma visual del PDF (RF-19): el nombre de quien aprobó esta versión del informe, tomado de
        // la revisión ya persistida. No se guarda nada nuevo — es el mismo dato que ya usa RF-17/RF-18,
        // resuelto aquí solo para imprimirlo en el documento.
        var approval = await context.EvaluationReportReviews.AsNoTracking()
            .Where(value => value.ReportId == report.Id && value.Decision == EvaluationReportDecisions.Approved)
            .OrderByDescending(value => value.ReviewedAt)
            .Select(value => new { value.ReviewedBy, value.SignatureName, value.ReviewedAt })
            .FirstOrDefaultAsync(cancellationToken);
        var approverName = approval is null ? null : await context.Users.AsNoTracking()
            .Where(user => user.Id == approval.ReviewedBy)
            .Select(user => user.FullName)
            .FirstOrDefaultAsync(cancellationToken);

        // La otra firma del documento: el técnico que emitió esta versión. Sale de la misma fila del
        // informe, así que no hay nada que resolver salvo su nombre registrado para la aclaración.
        var technicianName = await context.Users.AsNoTracking()
            .Where(user => user.Id == report.CreatedBy)
            .Select(user => user.FullName)
            .FirstOrDefaultAsync(cancellationToken);

        return new OfficialReportContent(
            instanceId,
            inspectionCase.Id,
            company.LegalName,
            company.Rnc,
            report.Version,
            report.ExecutiveSummary,
            report.Findings,
            report.Recommendations,
            result.BpmPercentage,
            result.QualificationCode,
            result.Classification,
            result.BpmRiskScore,
            result.FrequencyMonths,
            result.CriticalCount,
            result.MajorCount,
            result.MinorCount,
            nonConformities,
            evidences,
            report.CreatedAt,
            DateTimeOffset.UtcNow,
            string.IsNullOrWhiteSpace(approverName) ? "Coordinador EBR" : approverName,
            string.IsNullOrWhiteSpace(approval?.SignatureName) ? "Coordinador EBR" : approval.SignatureName,
            approval?.ReviewedAt ?? report.CreatedAt,
            string.IsNullOrWhiteSpace(technicianName) ? "Técnico Evaluador" : technicianName,
            report.SignatureName);
    }

    private static OfficialReportResponse DescribeOfficial(EvaluationOfficialReport value, int instanceId, int version) =>
        new(value.Id, instanceId, value.ReportId, version, value.FileName, value.MimeType, value.SizeBytes,
            value.Sha256, value.SignatureAlgorithm, value.PublicKeyThumbprint, value.GeneratedAt, value.GeneratedBy);

    private static ClosureResponse DescribeClosure(CaseClosure value) => new(value.Id, value.CaseId, value.ReportId,
        value.OfficialReportId, value.Status, value.Result, value.ClosedAt, value.ClosedBy);

    /// <summary>
    /// Observaciones de la revisión, de la versión más antigua a la más reciente (RF-18). Es lo que el
    /// técnico consulta para saber qué corregir antes de reenviar el informe.
    /// </summary>
    private static async Task<IResult> ListReviewsAsync(
        int id,
        ClaimsPrincipal principal,
        EbrDbContext context,
        CancellationToken cancellationToken)
    {
        var instance = await context.EvaluationInstances.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (instance is null) return Results.NotFound();
        if (!await CanReadAsync(principal, instance.CaseId, context, cancellationToken)) return Results.Forbid();

        var reviews = await (
            from review in context.EvaluationReportReviews.AsNoTracking()
            join report in context.EvaluationReports.AsNoTracking() on review.ReportId equals report.Id
            where report.EvaluationInstanceId == id
            orderby report.Version, review.ReviewedAt
            select new { Review = review, report.Version }).ToListAsync(cancellationToken);

        return Results.Ok(reviews.Select(item => Describe(item.Review, item.Version)));
    }

    private static EvaluationReportReviewResponse Describe(EvaluationReportReview review, int reportVersion) => new(
        review.Id,
        review.ReportId,
        reportVersion,
        review.Decision,
        review.Observations,
        review.SignatureName,
        review.ReviewedAt,
        review.ReviewedBy);

    private static EvaluationReportResponse Describe(EvaluationReport report) => new(
        report.Id,
        report.EvaluationInstanceId,
        report.Version,
        report.Status,
        report.ExecutiveSummary,
        report.Findings,
        report.Recommendations,
        report.SignatureName,
        report.CreatedAt,
        report.CreatedBy);

    private sealed record IssueReportRequest(
        string ExecutiveSummary, string Findings, string Recommendations, string? SignatureName);

    private sealed record ReviewReportRequest(string? Decision, string? Observations, string? SignatureName);
    private sealed record CloseCaseRequest(string? Result);

    private sealed record EvaluationReportReviewResponse(
        int Id,
        int ReportId,
        int ReportVersion,
        string Decision,
        string Observations,
        string? SignatureName,
        DateTimeOffset ReviewedAt,
        Guid ReviewedBy);

    private sealed record EvaluationReportResponse(
        int Id,
        int EvaluationInstanceId,
        int Version,
        string Status,
        string ExecutiveSummary,
        string Findings,
        string Recommendations,
        string SignatureName,
        DateTimeOffset CreatedAt,
        Guid CreatedBy);

    private sealed record OfficialReportResponse(int Id, int EvaluationInstanceId, int ReportId, int ReportVersion,
        string FileName, string MimeType, long SizeBytes, string Sha256, string SignatureAlgorithm,
        string PublicKeyThumbprint, DateTimeOffset GeneratedAt, Guid GeneratedBy);
    private sealed record ClosureResponse(int Id, int CaseId, int ReportId, int OfficialReportId, string Status,
        string Result, DateTimeOffset ClosedAt, Guid ClosedBy);
    private sealed record SignatureVerificationResponse(
        int OfficialReportId, int EvaluationInstanceId, bool HashMatches, bool SignatureValid, bool Valid,
        string SignatureAlgorithm, string PublicKeyThumbprint, DateTimeOffset GeneratedAt, Guid GeneratedBy);
}