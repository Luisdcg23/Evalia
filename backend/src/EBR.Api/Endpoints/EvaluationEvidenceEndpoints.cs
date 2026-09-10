using System.Security.Claims;
using System.Security.Cryptography;
using EBR.Application.Evidence;
using EBR.Domain.Evaluations;
using EBR.Domain.Identity;
using EBR.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EBR.Api.Endpoints;

/// <summary>
/// Evidencias de la evaluación en campo (fase 4.1). El binario se guarda en el almacenamiento de
/// objetos (<see cref="IEvidenceStorage"/>) y en PostgreSQL solo quedan los metadatos con la clave del
/// objeto.
/// </summary>
public static class EvaluationEvidenceEndpoints
{
    public static IEndpointRouteBuilder MapEvaluationEvidenceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/evaluations/{id:int}/evidence", UploadAsync)
            .WithTags("Evaluaciones")
            .DisableAntiforgery()
            .RequireAuthorization(policy => policy.RequireRole(SystemRoles.Evaluator));

        endpoints.MapGet("/api/evaluations/{id:int}/evidence", ListAsync)
            .WithTags("Evaluaciones")
            .RequireAuthorization(policy => policy.RequireRole(
                SystemRoles.Evaluator, SystemRoles.Coordinator, SystemRoles.Administrator));

        endpoints.MapGet("/api/evaluations/{id:int}/evidence/{evidenceId:int}/content", DownloadAsync)
            .WithTags("Evaluaciones")
            .RequireAuthorization(policy => policy.RequireRole(
                SystemRoles.Evaluator, SystemRoles.Coordinator, SystemRoles.Administrator));

        return endpoints;
    }

    /// <summary>Lista los metadatos de las evidencias de una evaluación, sin descargar binarios.</summary>
    private static async Task<IResult> ListAsync(
        int id,
        ClaimsPrincipal principal,
        EbrDbContext context,
        CancellationToken cancellationToken)
    {
        var instance = await context.EvaluationInstances.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (instance is null) return Results.NotFound();
        if (!await CanReadAsync(principal, context, instance.CaseId, cancellationToken)) return Results.Forbid();

        var evidences = await context.EvaluationEvidences.AsNoTracking()
            .Where(value => value.EvaluationInstanceId == id)
            .OrderBy(value => value.Id)
            .ToListAsync(cancellationToken);

        var payload = new List<EvaluationEvidenceResponse>(evidences.Count);
        foreach (var evidence in evidences)
        {
            payload.Add(await DescribeAsync(context, evidence, cancellationToken));
        }

        return Results.Ok(payload);
    }

    /// <summary>
    /// Devuelve el binario desde el almacenamiento de objetos. Si el metadato existe pero el objeto no
    /// está en el almacenamiento se responde <c>404</c>: no se inventa un archivo vacío.
    /// </summary>
    private static async Task<IResult> DownloadAsync(
        int id,
        int evidenceId,
        ClaimsPrincipal principal,
        EbrDbContext context,
        IEvidenceStorage storage,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var actingUserId)) return Results.Unauthorized();

        var evidence = await context.EvaluationEvidences.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == evidenceId && value.EvaluationInstanceId == id, cancellationToken);
        if (evidence is null) return Results.NotFound();

        var instance = await context.EvaluationInstances.AsNoTracking()
            .SingleAsync(value => value.Id == evidence.EvaluationInstanceId, cancellationToken);
        if (!await CanReadAsync(principal, context, instance.CaseId, cancellationToken)) return Results.Forbid();

        var content = await storage.OpenAsync(evidence.StorageKey, cancellationToken);
        if (content is null) return Results.NotFound();

        return Results.File(content, evidence.MimeType, evidence.FileName);
    }

    /// <summary>
    /// Sube una evidencia. El hash SHA-256 se calcula sobre los bytes recibidos y no se acepta del
    /// cliente: es lo que permite comprobar después que el objeto almacenado es el mismo que se
    /// adjuntó. Si se indica una pregunta, la evidencia queda ligada a su respuesta ya guardada.
    /// </summary>
    private static async Task<IResult> UploadAsync(
        int id,
        IFormFile file,
        [FromForm] int? templateItemId,
        [FromForm] string? description,
        ClaimsPrincipal principal,
        EbrDbContext context,
        IEvidenceStorage storage,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var actingUserId)) return Results.Unauthorized();

        var instance = await context.EvaluationInstances.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (instance is null) return Results.NotFound();

        // Una evaluación enviada es inmutable: adjuntar evidencias después alteraría el sustento del
        // resultado ya calculado, así que se rechaza igual que la modificación de respuestas (RF-13).
        if (instance.Status == EvaluationInstanceStatuses.Submitted)
            return Results.Conflict(new { message = "La evaluación ya fue enviada y no admite nuevas evidencias." });

        if (!await IsAssignedTechnicianAsync(context, instance.CaseId, actingUserId, cancellationToken)) return Results.Forbid();

        var errors = new Dictionary<string, string[]>();
        if (file.Length <= 0) errors["file"] = ["El archivo de evidencia está vacío."];
        else if (file.Length > EvidencePolicy.MaxSizeBytes)
            errors["file"] = [$"La evidencia supera el tamaño máximo permitido de {EvidencePolicy.MaxSizeBytes / (1024 * 1024)} MB."];

        var mimeType = file.ContentType?.Trim() ?? string.Empty;
        if (!EvidencePolicy.IsAllowed(mimeType))
        {
            errors["mimeType"] = [$"El tipo {mimeType} no se admite como evidencia. Tipos permitidos: " +
                $"{string.Join(", ", EvidencePolicy.AllowedMimeTypes)}."];
        }

        int? responseId = null;
        if (templateItemId is not null)
        {
            responseId = await context.EvaluationResponses.AsNoTracking()
                .Where(value => value.EvaluationInstanceId == id && value.TemplateItemId == templateItemId)
                .Select(value => (int?)value.Id)
                .SingleOrDefaultAsync(cancellationToken);
            if (responseId is null)
                errors["templateItemId"] = ["Guarda la respuesta de la pregunta antes de adjuntarle una evidencia."];
        }

        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var fileName = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(fileName)) fileName = "evidencia";
        if (fileName.Length > 260) fileName = fileName[^260..];

        using var buffer = new MemoryStream();
        await using (var upload = file.OpenReadStream())
        {
            await upload.CopyToAsync(buffer, cancellationToken);
        }

        buffer.Position = 0;
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(buffer, cancellationToken)).ToLowerInvariant();
        buffer.Position = 0;

        var storageKey = $"evaluaciones/{instance.Id}/{Guid.NewGuid():N}{Path.GetExtension(fileName)}";
        await storage.SaveAsync(storageKey, mimeType, buffer, cancellationToken);

        var evidence = new EvaluationEvidence
        {
            EvaluationInstanceId = instance.Id,
            EvaluationResponseId = responseId,
            FileName = fileName,
            MimeType = mimeType,
            SizeBytes = buffer.Length,
            Hash = hash,
            StorageKey = storageKey,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            UploadedBy = actingUserId
        };
        context.EvaluationEvidences.Add(evidence);
        await context.SaveChangesAsync(cancellationToken);

        var payload = await DescribeAsync(context, evidence, cancellationToken);
        return Results.Created($"/api/evaluations/{instance.Id}/evidence/{evidence.Id}/content", payload);
    }

    /// <summary>
    /// El técnico solo actúa sobre el expediente que tiene asignado vigente, la misma regla que gobierna
    /// la captura de respuestas: una reasignación en curso traslada también el acceso a las evidencias.
    /// </summary>
    /// <summary>
    /// Coordinador y Administrador revisan cualquier expediente; el Técnico Evaluador solo el que tiene
    /// asignado vigente. La empresa inspeccionada no accede a las evidencias de campo: recibe el informe
    /// oficial, que es el documento con validez frente a ella.
    /// </summary>
    private static async Task<bool> CanReadAsync(
        ClaimsPrincipal principal,
        EbrDbContext context,
        int caseId,
        CancellationToken cancellationToken)
    {
        if (principal.IsInRole(SystemRoles.Coordinator) || principal.IsInRole(SystemRoles.Administrator)) return true;
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var actingUserId)) return false;
        return await IsAssignedTechnicianAsync(context, caseId, actingUserId, cancellationToken);
    }

    private static async Task<bool> IsAssignedTechnicianAsync(
        EbrDbContext context,
        int caseId,
        Guid actingUserId,
        CancellationToken cancellationToken)
    {
        var assignment = await context.CaseAssignments.AsNoTracking()
            .SingleOrDefaultAsync(value => value.CaseId == caseId && value.IsCurrent, cancellationToken);
        return assignment is not null && assignment.TechnicianId == actingUserId;
    }

    private static async Task<EvaluationEvidenceResponse> DescribeAsync(
        EbrDbContext context,
        EvaluationEvidence evidence,
        CancellationToken cancellationToken)
    {
        int? templateItemId = null;
        if (evidence.EvaluationResponseId is not null)
        {
            templateItemId = await context.EvaluationResponses.AsNoTracking()
                .Where(value => value.Id == evidence.EvaluationResponseId)
                .Select(value => (int?)value.TemplateItemId)
                .SingleAsync(cancellationToken);
        }

        return new EvaluationEvidenceResponse(
            evidence.Id,
            evidence.EvaluationInstanceId,
            evidence.EvaluationResponseId,
            templateItemId,
            evidence.FileName,
            evidence.MimeType,
            evidence.SizeBytes,
            evidence.Hash,
            evidence.StorageKey,
            evidence.Description,
            evidence.UploadedAt,
            evidence.UploadedBy);
    }

    private sealed record EvaluationEvidenceResponse(
        int Id,
        int EvaluationInstanceId,
        int? EvaluationResponseId,
        int? TemplateItemId,
        string FileName,
        string MimeType,
        long SizeBytes,
        string Hash,
        string StorageKey,
        string? Description,
        DateTimeOffset UploadedAt,
        Guid UploadedBy);
}
