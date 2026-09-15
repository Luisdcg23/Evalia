using System.Security.Claims;
using EBR.Domain.Evaluations;
using EBR.Domain.Identity;
using EBR.Infrastructure.Persistence;
using EBR.Infrastructure.Risk;
using Microsoft.EntityFrameworkCore;

namespace EBR.Api.Endpoints;

public static class EvaluationTemplateEndpoints
{
    public static IEndpointRouteBuilder MapEvaluationTemplateEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // El grupo admite por defecto a Administrador, Coordinador y Técnico Evaluador, porque el técnico
        // necesita leer el árbol de preguntas para responder la ficha en campo y el coordinador para dar
        // seguimiento. Las rutas de ESCRITURA (crear/editar/borrar ítems, publicar, versionar y crear
        // plantilla) restringen ese acceso a solo Administrador con una segunda llamada a
        // `RequireAuthorization`: como las políticas de un mismo endpoint se combinan con AND, esa segunda
        // llamada actúa como intersección (angosta el conjunto del grupo, nunca lo amplía) — mismo patrón
        // de "narrowing" que ya usan `CaseEndpoints` (p. ej. `AssignAsync`) y `RiskCatalogEndpoints`
        // (las rutas `POST` de catálogos). Solo `GET /` y `GET /{id}/items` (lectura) se quedan con la
        // política amplia del grupo.
        var group = endpoints.MapGroup("/api/evaluation-templates").WithTags("Plantillas de evaluación")
            .RequireAuthorization(policy => policy.RequireRole(
                SystemRoles.Administrator, SystemRoles.Coordinator, SystemRoles.Evaluator));
        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync)
            .RequireAuthorization(policy => policy.RequireRole(SystemRoles.Administrator));
        group.MapGet("/active", GetActiveAsync);
        group.MapPost("/{id:int}/activate", ActivateAsync)
            .RequireAuthorization(policy => policy.RequireRole(SystemRoles.Administrator));
        group.MapGet("/{id:int}/items", ListItemsAsync);
        group.MapPost("/{id:int}/items", CreateItemAsync)
            .RequireAuthorization(policy => policy.RequireRole(SystemRoles.Administrator));
        group.MapPut("/{id:int}/items/{itemId:int}", UpdateItemAsync)
            .RequireAuthorization(policy => policy.RequireRole(SystemRoles.Administrator));
        group.MapDelete("/{id:int}/items/{itemId:int}", DeleteItemAsync)
            .RequireAuthorization(policy => policy.RequireRole(SystemRoles.Administrator));
        group.MapPost("/{id:int}/publish", PublishAsync)
            .RequireAuthorization(policy => policy.RequireRole(SystemRoles.Administrator));
        group.MapPost("/{id:int}/versions", CreateVersionAsync)
            .RequireAuthorization(policy => policy.RequireRole(SystemRoles.Administrator));
        return endpoints;
    }

    private static async Task<IResult> ListAsync(EbrDbContext context, CancellationToken cancellationToken) =>
        Results.Ok(await context.EvaluationTemplates.AsNoTracking()
            .OrderBy(item => item.Name).ThenByDescending(item => item.Version).ToListAsync(cancellationToken));

    /// <summary>
    /// Crea una plantilla en borrador. Siembra de inmediato las cuatro opciones evaluables estándar de
    /// la ficha BPM (<see cref="BpmResponseOptions"/>: <c>C</c>/<c>CP</c>/<c>IT</c>/<c>NA</c>) —no es un
    /// dato normativo inventado aquí, es el mismo contrato fijo que ya documenta <c>DATABASE.md</c> y
    /// que reproducía manualmente cada prueba antes de este cambio— para que una plantilla nueva pueda
    /// empezar a capturar respuestas sin un paso de configuración aparte.
    /// </summary>
    private static async Task<IResult> CreateAsync(CreateTemplateRequest request, EbrDbContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return Validation("El nombre de la plantilla es obligatorio.");
        var template = new EvaluationTemplate { Name = request.Name.Trim() };
        context.EvaluationTemplates.Add(template);
        await context.SaveChangesAsync(cancellationToken);

        var order = 1;
        foreach (var option in BpmResponseOptions.All)
        {
            context.EvaluationResponseOptions.Add(new EvaluationResponseOption
            {
                TemplateId = template.Id,
                Code = option.Code,
                Name = option.Name,
                Value = option.Value,
                CountsTowardDenominator = option.CountsTowardDenominator,
                Order = order++
            });
        }
        await context.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/evaluation-templates/{template.Id}", template);
    }

    private static async Task<IResult> ListItemsAsync(int id, EbrDbContext context, CancellationToken cancellationToken)
    {
        if (!await context.EvaluationTemplates.AnyAsync(item => item.Id == id, cancellationToken)) return Results.NotFound();
        return Results.Ok(await context.EvaluationTemplateItems.AsNoTracking().Where(item => item.TemplateId == id && item.IsActive)
            .OrderBy(item => item.Code).ThenBy(item => item.Order).ToListAsync(cancellationToken));
    }

    private static async Task<IResult> CreateItemAsync(int id, UpsertItemRequest request, EbrDbContext context, CancellationToken cancellationToken)
    {
        var template = await context.EvaluationTemplates.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (template is null) return Results.NotFound();
        if (template.Status != EvaluationTemplateStatuses.Draft) return Locked();
        var error = await ValidateItemAsync(id, null, request, context, cancellationToken);
        if (error is not null) return error;
        var item = MapRequest(id, request);
        context.EvaluationTemplateItems.Add(item);
        await context.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/evaluation-templates/{id}/items/{item.Id}", item);
    }

    private static async Task<IResult> UpdateItemAsync(int id, int itemId, UpsertItemRequest request, EbrDbContext context, CancellationToken cancellationToken)
    {
        var template = await context.EvaluationTemplates.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (template is null) return Results.NotFound();
        if (template.Status != EvaluationTemplateStatuses.Draft) return Locked();
        var item = await context.EvaluationTemplateItems.SingleOrDefaultAsync(value => value.Id == itemId && value.TemplateId == id, cancellationToken);
        if (item is null) return Results.NotFound();
        if (request.VersionToken.HasValue && request.VersionToken != item.VersionToken)
            return Results.Conflict(new { message = "El ítem fue modificado por otro usuario." });
        var error = await ValidateItemAsync(id, itemId, request, context, cancellationToken);
        if (error is not null) return error;
        ApplyRequest(item, request);
        item.VersionToken = Guid.NewGuid();
        await context.SaveChangesAsync(cancellationToken);
        return Results.Ok(item);
    }

    private static async Task<IResult> DeleteItemAsync(int id, int itemId, EbrDbContext context, CancellationToken cancellationToken)
    {
        var template = await context.EvaluationTemplates.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (template is null) return Results.NotFound();
        if (template.Status != EvaluationTemplateStatuses.Draft) return Locked();
        var item = await context.EvaluationTemplateItems.SingleOrDefaultAsync(value => value.Id == itemId && value.TemplateId == id, cancellationToken);
        if (item is null) return Results.NotFound();
        if (await context.EvaluationTemplateItems.AnyAsync(value => value.ParentId == itemId && value.IsActive, cancellationToken))
            return Results.Conflict(new { message = "Mueva o elimine primero los ítems dependientes." });
        item.IsActive = false;
        item.VersionToken = Guid.NewGuid();
        await context.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> PublishAsync(
        int id, EbrDbContext context, RiskRuleVersionProvisioner provisioner, CancellationToken cancellationToken)
    {
        var template = await context.EvaluationTemplates.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (template is null) return Results.NotFound();
        if (template.Status != EvaluationTemplateStatuses.Draft) return Results.Conflict(new { message = "La plantilla ya fue publicada." });
        if (!await context.EvaluationTemplateItems.AnyAsync(item => item.TemplateId == id && item.IsActive, cancellationToken))
            return Results.Conflict(new { message = "La plantilla debe contener al menos un ítem." });

        var bandsError = await ValidateQualificationBandsAsync(id, context, provisioner, cancellationToken);
        if (bandsError is not null) return bandsError;

        template.Status = EvaluationTemplateStatuses.Published;
        template.PublishedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return Results.Ok(template);
    }

    /// <summary>
    /// Verifica el contrato mínimo que permite calificar una evaluación de esta plantilla sin que
    /// <c>EvaluationSubmissionService.BpmFactorOptionAsync</c> lance una excepción no controlada al
    /// enviarla: el número de <see cref="EvaluationQualificationRule"/> (bandas de calificación) debe
    /// coincidir exactamente con el número de opciones del factor estructural <c>BPM</c> de la versión
    /// de reglas de riesgo publicada vigente, porque la correspondencia entre banda y opción se hace
    /// por posición ordinal (ver esa clase). <strong>Solo se valida si ya existe una versión de reglas
    /// publicada</strong>: si todavía no existe ninguna, <c>StartAsync</c> ya bloquea el inicio de
    /// cualquier evaluación con un 409 propio, así que no hay riesgo de la excepción no controlada y no
    /// tiene sentido bloquear la publicación de plantillas contra un catálogo de riesgo que ni siquiera
    /// existe todavía (por ejemplo, en un despliegue nuevo que aún no cargó la matriz de riesgo).
    /// </summary>
    private static async Task<IResult?> ValidateQualificationBandsAsync(
        int templateId, EbrDbContext context, RiskRuleVersionProvisioner provisioner, CancellationToken cancellationToken)
    {
        var ruleVersion = await provisioner.FindPublishedAsync(cancellationToken);
        if (ruleVersion is null) return null;

        var bpmOptionsCount = await context.StructuralRiskFactors.AsNoTracking()
            .Where(factor => factor.RuleVersionId == ruleVersion.Id && factor.IsActive && factor.Code == RiskCatalogSeeder.BpmFactorCode)
            .SelectMany(factor => factor.Options)
            .CountAsync(cancellationToken);
        if (bpmOptionsCount == 0) return null;

        var rulesCount = await context.EvaluationQualificationRules.CountAsync(rule => rule.TemplateId == templateId, cancellationToken);
        if (rulesCount == bpmOptionsCount) return null;

        var difference = bpmOptionsCount - rulesCount;
        var detail = difference > 0
            ? $"faltan {difference} banda(s)"
            : $"sobran {-difference} banda(s)";
        return Results.Conflict(new
        {
            message = $"La plantilla declara {rulesCount} banda(s) de calificación, pero el factor BPM vigente de la " +
                $"matriz de riesgo tiene {bpmOptionsCount} opción(es): deben coincidir exactamente ({detail})."
        });
    }

    /// <summary>
    /// Devuelve cuál es la plantilla activa hoy para <c>POST /api/cases/{id}/evaluations</c>
    /// (<see cref="ActiveEvaluationTemplate"/>), o <c>templateId: null</c> si nunca se ha activado
    /// ninguna. Distinto de "la más recientemente publicada": ver <see cref="ActivateAsync"/>.
    /// </summary>
    private static async Task<IResult> GetActiveAsync(EbrDbContext context, CancellationToken cancellationToken)
    {
        var active = await context.ActiveEvaluationTemplates.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == ActiveEvaluationTemplate.SingletonId, cancellationToken);
        return Results.Ok(new { templateId = active?.TemplateId, activatedAt = active?.ActivatedAt });
    }

    /// <summary>
    /// Marca una plantilla publicada como la que usará <c>StartAsync</c> para iniciar evaluaciones
    /// nuevas, reemplazando la que estuviera activa antes (fila única en <c>Plantilla_Activa</c>).
    /// Rechaza plantillas que no estén <c>PUBLISHED</c> y, sobre todo, rechaza (nunca activa "con
    /// confirmación forzada") una plantilla que no cumpla el mismo contrato mínimo que exige
    /// <see cref="PublishAsync"/>: activar por error una plantilla cuyas bandas no coinciden con el
    /// factor BPM vigente rompería <em>cualquier</em> evaluación que se envíe mientras esté activa, así
    /// que aquí se prefiere un 409 explícito sobre la posibilidad de dejar el sistema en un estado
    /// inconsistente.
    /// </summary>
    private static async Task<IResult> ActivateAsync(
        int id, ClaimsPrincipal principal, EbrDbContext context, RiskRuleVersionProvisioner provisioner, CancellationToken cancellationToken)
    {
        var template = await context.EvaluationTemplates.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (template is null) return Results.NotFound();
        if (template.Status != EvaluationTemplateStatuses.Published)
            return Results.Conflict(new { message = "Solo una plantilla publicada puede activarse para evaluaciones." });

        var bandsError = await ValidateQualificationBandsAsync(id, context, provisioner, cancellationToken);
        if (bandsError is not null) return bandsError;

        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var actingUserId)) return Results.Unauthorized();

        var active = await context.ActiveEvaluationTemplates.SingleOrDefaultAsync(
            item => item.Id == ActiveEvaluationTemplate.SingletonId, cancellationToken);
        if (active is null)
        {
            active = new ActiveEvaluationTemplate { TemplateId = id, ActivatedBy = actingUserId };
            context.ActiveEvaluationTemplates.Add(active);
        }
        else
        {
            active.TemplateId = id;
            active.ActivatedAt = DateTimeOffset.UtcNow;
            active.ActivatedBy = actingUserId;
        }
        await context.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { templateId = active.TemplateId, activatedAt = active.ActivatedAt });
    }

    private static async Task<IResult> CreateVersionAsync(int id, EbrDbContext context, CancellationToken cancellationToken)
    {
        var source = await context.EvaluationTemplates.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (source is null) return Results.NotFound();
        if (source.Status != EvaluationTemplateStatuses.Published)
            return Results.Conflict(new { message = "Solo una versión publicada puede clonarse." });
        var nextVersion = await context.EvaluationTemplates.Where(item => item.FamilyId == source.FamilyId)
            .MaxAsync(item => item.Version, cancellationToken) + 1;
        var clone = new EvaluationTemplate { FamilyId = source.FamilyId, Name = source.Name, Version = nextVersion };
        context.EvaluationTemplates.Add(clone);
        await context.SaveChangesAsync(cancellationToken);
        var sourceItems = await context.EvaluationTemplateItems.AsNoTracking().Where(item => item.TemplateId == id && item.IsActive)
            .OrderBy(item => item.Id).ToListAsync(cancellationToken);
        var idMap = new Dictionary<int, int>();
        foreach (var sourceItem in sourceItems)
        {
            var clonedItem = CloneItem(sourceItem, clone.Id, sourceItem.ParentId.HasValue ? idMap[sourceItem.ParentId.Value] : null);
            context.EvaluationTemplateItems.Add(clonedItem);
            await context.SaveChangesAsync(cancellationToken);
            idMap[sourceItem.Id] = clonedItem.Id;
        }
        return Results.Created($"/api/evaluation-templates/{clone.Id}", clone);
    }

    private static async Task<IResult?> ValidateItemAsync(int templateId, int? itemId, UpsertItemRequest request, EbrDbContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Description) ||
            !EvaluationItemTypes.All.Contains(request.ItemType.Trim().ToUpperInvariant()))
            return Validation("Código, descripción y un tipo de ítem válido son obligatorios.");
        if (itemId.HasValue && request.ParentId == itemId) return Validation("Un ítem no puede ser su propio padre.");
        if (request.ParentId.HasValue && !await context.EvaluationTemplateItems.AnyAsync(
            item => item.Id == request.ParentId && item.TemplateId == templateId && item.IsActive, cancellationToken))
            return Validation("El padre debe pertenecer a la misma plantilla.");
        if (await context.EvaluationTemplateItems.AnyAsync(item => item.TemplateId == templateId && item.Code == request.Code.Trim() && item.Id != itemId, cancellationToken))
            return Results.Conflict(new { message = "El código ya existe en esta versión." });
        return null;
    }

    private static EvaluationTemplateItem MapRequest(int templateId, UpsertItemRequest request)
    {
        var item = new EvaluationTemplateItem { TemplateId = templateId, Code = request.Code, Description = request.Description, ItemType = request.ItemType };
        ApplyRequest(item, request);
        return item;
    }

    private static void ApplyRequest(EvaluationTemplateItem item, UpsertItemRequest request)
    {
        item.ParentId = request.ParentId;
        item.Code = request.Code.Trim();
        item.Description = request.Description.Trim();
        item.ItemType = request.ItemType.Trim().ToUpperInvariant();
        item.Order = request.Order;
        item.Weight = request.Weight;
        item.IsRequired = request.IsRequired;
        item.IsCritical = request.IsCritical;
        item.AllowsNotApplicable = request.AllowsNotApplicable;
        item.ResponseType = request.ResponseType?.Trim().ToUpperInvariant();
        item.RulesJson = string.IsNullOrWhiteSpace(request.RulesJson) ? "{}" : request.RulesJson;
        item.ScoreConfigurationJson = string.IsNullOrWhiteSpace(request.ScoreConfigurationJson) ? "{}" : request.ScoreConfigurationJson;
    }

    private static EvaluationTemplateItem CloneItem(EvaluationTemplateItem source, int templateId, int? parentId) => new()
    {
        TemplateId = templateId, ParentId = parentId, Code = source.Code, Description = source.Description,
        ItemType = source.ItemType, Order = source.Order, Weight = source.Weight, IsRequired = source.IsRequired,
        IsCritical = source.IsCritical, AllowsNotApplicable = source.AllowsNotApplicable, ResponseType = source.ResponseType,
        RulesJson = source.RulesJson, ScoreConfigurationJson = source.ScoreConfigurationJson
    };

    private static IResult Locked() => Results.Conflict(new { message = "La versión publicada es inmutable." });
    private static IResult Validation(string message) => Results.ValidationProblem(new Dictionary<string, string[]> { ["template"] = [message] });

    private sealed record CreateTemplateRequest(string Name);
    private sealed record UpsertItemRequest(string Code, string Description, string ItemType, int Order,
        int? ParentId = null, decimal? Weight = null, bool IsRequired = false, bool IsCritical = false, bool AllowsNotApplicable = true,
        string? ResponseType = null, string? RulesJson = null, string? ScoreConfigurationJson = null, Guid? VersionToken = null);
}
