using EBR.Domain.Evaluations;
using EBR.Infrastructure.Evaluations;
using EBR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EBR.IntegrationTests;

public sealed class BpmTemplateSeedTests
{
    [Fact]
    public async Task SeedBuildsTheCompleteTreeOfTheOfficialForm()
    {
        await using var context = NewContext();

        var result = await new BpmTemplateSeeder(context).SeedAsync(CancellationToken.None);

        Assert.Equal(90, result.Items);
        Assert.Equal(7, await CountAsync(context, result.TemplateId, "CHAPTER"));
        Assert.Equal(13, await CountAsync(context, result.TemplateId, "SECTION"));
        Assert.Equal(19, await CountAsync(context, result.TemplateId, "SUBSECTION"));
        Assert.Equal(6, await CountAsync(context, result.TemplateId, "GROUP"));
        Assert.Equal(45, await CountAsync(context, result.TemplateId, "QUESTION"));
    }

    [Fact]
    public async Task EveryQuestionWeighsOnePointAndTheFormAddsUpToFortyFive()
    {
        await using var context = NewContext();

        var result = await new BpmTemplateSeeder(context).SeedAsync(CancellationToken.None);

        var questions = await context.EvaluationTemplateItems
            .Where(item => item.TemplateId == result.TemplateId && item.ItemType == "QUESTION")
            .ToListAsync();
        Assert.All(questions, question => Assert.Equal(1m, question.Weight));
        Assert.All(questions, question => Assert.True(question.AllowsNotApplicable));
        Assert.Equal(45m, questions.Sum(question => question.Weight));
    }

    [Fact]
    public async Task CodesAreUniqueWithinTheTemplate()
    {
        await using var context = NewContext();

        var result = await new BpmTemplateSeeder(context).SeedAsync(CancellationToken.None);

        var codes = await context.EvaluationTemplateItems
            .Where(item => item.TemplateId == result.TemplateId)
            .Select(item => item.Code)
            .ToListAsync();
        Assert.Equal(codes.Count, codes.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task EveryParentBelongsToTheSameTemplateAndTheTreeHasNoCycles()
    {
        await using var context = NewContext();

        var result = await new BpmTemplateSeeder(context).SeedAsync(CancellationToken.None);

        var items = await context.EvaluationTemplateItems.AsNoTracking().ToListAsync();
        var byId = items.ToDictionary(item => item.Id);
        Assert.All(items, item =>
        {
            if (!item.ParentId.HasValue) return;
            Assert.True(byId.ContainsKey(item.ParentId.Value));
            Assert.Equal(item.TemplateId, byId[item.ParentId.Value].TemplateId);
        });

        foreach (var item in items)
        {
            var visited = new HashSet<int> { item.Id };
            var current = item;
            while (current.ParentId.HasValue)
            {
                Assert.True(visited.Add(current.ParentId.Value), $"El nodo {item.Code} participa en un ciclo.");
                current = byId[current.ParentId.Value];
            }
        }

        Assert.Equal(7, items.Count(item => item.ParentId is null));
    }

    [Fact]
    public async Task DescriptionsKeepTheCompleteTextOfTheForm()
    {
        await using var context = NewContext();

        var result = await new BpmTemplateSeeder(context).SeedAsync(CancellationToken.None);

        var descriptions = await context.EvaluationTemplateItems
            .Where(item => item.TemplateId == result.TemplateId)
            .ToDictionaryAsync(item => item.Code, item => item.Description);
        // El SQL heredado truncaba a 255 caracteres. Aquí conviven textos más largos y el único
        // texto de 255 caracteres exactos de la ficha se conserva completo.
        Assert.Equal(5, descriptions.Values.Count(description => description.Length > 255));
        Assert.Equal(
            "El establecimiento mantiene un sistema de control para asegurar que las materias primas y " +
            "otros ingredientes a ser utilizados en la elaboración de alimentos son conformes con las " +
            "especificaciones de calidad e inocuidad establecidas en las espcificaciones.",
            descriptions["5.2.2.a"]);
        Assert.Equal(255, descriptions["5.2.2.a"].Length);
        Assert.Equal(
            "b) Los residuos sólidos son recogidos y eliminados por personal calificado y deben ser " +
            "depositados en contenedores debidamente identificados, construidos con material impermeable, " +
            "ubicados en áreas que eviten la infestación por plagas y cuando corresponda, se podrán cerrar " +
            "con llave para evitar la contaminación accidental o intencionada de los alimentos.",
            descriptions["1.2.1.b"]);
        Assert.True(descriptions["1.2.1.b"].Length > 255);
        Assert.Equal("5. . CONTROL DE LAS OPERACIONES", descriptions["5"]);
        Assert.Equal("3.1.3 Monitoreo/seguimiento de la eficacia", descriptions["3.1.3"]);
        Assert.Equal(
            "Las paredes deben tener una superficie lisa adecuada a las actividades que se realicen, " +
            "construídas con materiales impermeables de fácil limpieza y, cuando sea necesario, de fácil desinfección.",
            descriptions["1.1.3.1.a"]);
    }

    [Fact]
    public async Task ResponseOptionsFollowTheOfficialScale()
    {
        await using var context = NewContext();

        var result = await new BpmTemplateSeeder(context).SeedAsync(CancellationToken.None);

        var options = await context.EvaluationResponseOptions
            .Where(option => option.TemplateId == result.TemplateId)
            .OrderBy(option => option.Order)
            .ToListAsync();
        Assert.Equal(["C", "CP", "IT", "NA"], options.Select(option => option.Code));
        Assert.Equal(1m, options.Single(option => option.Code == "C").Value);
        Assert.Equal(0.5m, options.Single(option => option.Code == "CP").Value);
        Assert.Equal(0m, options.Single(option => option.Code == "IT").Value);
        Assert.Null(options.Single(option => option.Code == "NA").Value);
        Assert.False(options.Single(option => option.Code == "NA").CountsTowardDenominator);
        Assert.All(options.Where(option => option.Code != "NA"), option => Assert.True(option.CountsTowardDenominator));
    }

    [Fact]
    public async Task GuidanceCriteriaKeepTheirCriticalityAndSourceReference()
    {
        await using var context = NewContext();

        var result = await new BpmTemplateSeeder(context).SeedAsync(CancellationToken.None);

        var criteria = await context.EvaluationGuidanceCriteria.AsNoTracking().ToListAsync();
        Assert.Equal(162, criteria.Count);
        Assert.Equal(result.Criteria, criteria.Count);
        Assert.All(criteria, criterion =>
        {
            Assert.Equal("Guía de Llenado", criterion.SourceSheet);
            Assert.Matches("^[A-Z]{1,2}[0-9]{1,4}$", criterion.SourceCell);
            Assert.NotEqual(0, criterion.ItemId);
            Assert.True(criterion.Criticality is null
                or EvaluationCriticalityLevels.Critical
                or EvaluationCriticalityLevels.Major
                or EvaluationCriticalityLevels.Minor);
        });

        var items = await context.EvaluationTemplateItems.AsNoTracking().ToDictionaryAsync(item => item.Id, item => item.Code);
        var water = criteria.Where(criterion => items[criterion.ItemId] == "5.3.a").OrderBy(criterion => criterion.Order).ToList();
        Assert.Equal(6, water.Count);
        Assert.Equal(
            "i. El establecimiento mantiene el control y registro de la potabilidad del agua.",
            water[0].Description);
        Assert.Equal(EvaluationCriticalityLevels.Critical, water[0].Criticality);
        Assert.Equal("B394", water[0].SourceCell);
        Assert.Equal(EvaluationCriticalityLevels.Major, water[4].Criticality);
    }

    [Fact]
    public async Task QualificationRulesReproduceTheScoringBandsOfTheForm()
    {
        await using var context = NewContext();

        var result = await new BpmTemplateSeeder(context).SeedAsync(CancellationToken.None);

        var rules = await context.EvaluationQualificationRules
            .Where(rule => rule.TemplateId == result.TemplateId)
            .OrderBy(rule => rule.Order)
            .ToListAsync();
        Assert.Equal(4, rules.Count);
        Assert.Equal("Condiciones inaceptables", rules[0].Classification);
        Assert.Equal(60m, rules[0].MaxPercentage);
        Assert.True(rules[0].MaxIncluded);
        Assert.Null(rules[0].MinPercentage);
        Assert.Equal(80m, rules[3].MinPercentage);
        Assert.False(rules[3].MinIncluded);
        Assert.Null(rules[3].MaxPercentage);
    }

    [Fact]
    public async Task SeedIsIdempotentAndPublishesASingleVersion()
    {
        await using var context = NewContext();
        var seeder = new BpmTemplateSeeder(context);

        var first = await seeder.SeedAsync(CancellationToken.None);
        var second = await seeder.SeedAsync(CancellationToken.None);

        Assert.Equal(first.TemplateId, second.TemplateId);
        Assert.False(second.Created);
        Assert.Single(await context.EvaluationTemplates.ToListAsync());
        Assert.Equal(90, await context.EvaluationTemplateItems.CountAsync());
        Assert.Equal(4, await context.EvaluationResponseOptions.CountAsync());
        Assert.Equal(162, await context.EvaluationGuidanceCriteria.CountAsync());
        Assert.Equal(EvaluationTemplateStatuses.Published,
            (await context.EvaluationTemplates.SingleAsync()).Status);
    }

    [Fact]
    public async Task APublishedTemplateIsImmutable()
    {
        await using var context = NewContext();
        var result = await new BpmTemplateSeeder(context).SeedAsync(CancellationToken.None);

        var item = await context.EvaluationTemplateItems
            .FirstAsync(value => value.TemplateId == result.TemplateId && value.ItemType == "QUESTION");
        item.Description = "Texto alterado";

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
        Assert.Contains("inmutable", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task APublishedTemplateRejectsNewItems()
    {
        await using var context = NewContext();
        var result = await new BpmTemplateSeeder(context).SeedAsync(CancellationToken.None);

        context.EvaluationTemplateItems.Add(new EvaluationTemplateItem
        {
            TemplateId = result.TemplateId,
            Code = "8",
            Description = "Capítulo agregado fuera de la ficha",
            ItemType = "CHAPTER",
            Order = 1000
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    private static Task<int> CountAsync(EbrDbContext context, int templateId, string itemType) =>
        context.EvaluationTemplateItems.CountAsync(item => item.TemplateId == templateId && item.ItemType == itemType);

    private static EbrDbContext NewContext() =>
        new(new DbContextOptionsBuilder<EbrDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
