using EBR.Domain.Evaluations;
using EBR.Domain.Identity;
using EBR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EBR.Api.Endpoints;

public static class EvaluationTemplateEndpoints
{
    public static IEndpointRouteBuilder MapEvaluationTemplateEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/evaluation-templates").WithTags("Plantillas de evaluación")
            .RequireAuthorization(policy => policy.RequireRole(SystemRoles.Administrator));
        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapGet("/{id:int}/items", ListItemsAsync);
        group.MapPost("/{id:int}/items", CreateItemAsync);
        group.MapPut("/{id:int}/items/{itemId:int}", UpdateItemAsync);
        group.MapDelete("/{id:int}/items/{itemId:int}", DeleteItemAsync);
        group.MapPost("/{id:int}/publish", PublishAsync);
        group.MapPost("/{id:int}/versions", CreateVersionAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(EbrDbContext context, CancellationToken cancellationToken) =>
        Results.Ok(await context.EvaluationTemplates.AsNoTracking()
            .OrderBy(item => item.Name).ThenByDescending(item => item.Version).ToListAsync(cancellationToken));

    private static async Task<IResult> CreateAsync(CreateTemplateRequest request, EbrDbContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return Validation("El nombre de la plantilla es obligatorio.");
        var template = new EvaluationTemplate { Name = request.Name.Trim() };
        context.EvaluationTemplates.Add(template);
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

    private static async Task<IResult> PublishAsync(int id, EbrDbContext context, CancellationToken cancellationToken)
    {
        var template = await context.EvaluationTemplates.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (template is null) return Results.NotFound();
        if (template.Status != EvaluationTemplateStatuses.Draft) return Results.Conflict(new { message = "La plantilla ya fue publicada." });
        if (!await context.EvaluationTemplateItems.AnyAsync(item => item.TemplateId == id && item.IsActive, cancellationToken))
            return Results.Conflict(new { message = "La plantilla debe contener al menos un ítem." });
        template.Status = EvaluationTemplateStatuses.Published;
        template.PublishedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return Results.Ok(template);
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
