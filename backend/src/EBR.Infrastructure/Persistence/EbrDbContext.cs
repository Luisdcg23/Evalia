using EBR.Infrastructure.Identity;
using EBR.Domain.Companies;
using EBR.Domain.RiskCatalogs;
using EBR.Domain.Evaluations;
using EBR.Domain.Workflow;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EBR.Infrastructure.Persistence;

public sealed class EbrDbContext(DbContextOptions<EbrDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<PasswordRecoveryCode> PasswordRecoveryCodes => Set<PasswordRecoveryCode>();

    public DbSet<Company> Companies => Set<Company>();

    public DbSet<CompanyUser> CompanyUsers => Set<CompanyUser>();

    public DbSet<CompanyRepresentative> CompanyRepresentatives => Set<CompanyRepresentative>();

    public DbSet<RiskLevel> RiskLevels => Set<RiskLevel>();

    public DbSet<FoodCategory> FoodCategories => Set<FoodCategory>();
    public DbSet<FoodSubcategory> FoodSubcategories => Set<FoodSubcategory>();
    public DbSet<CompanyFoodSubcategory> CompanyFoodSubcategories => Set<CompanyFoodSubcategory>();
    public DbSet<StructuralRiskFactor> StructuralRiskFactors => Set<StructuralRiskFactor>();
    public DbSet<StructuralRiskOption> StructuralRiskOptions => Set<StructuralRiskOption>();
    public DbSet<CompanyRiskFactorValue> CompanyRiskFactorValues => Set<CompanyRiskFactorValue>();
    public DbSet<InspectionFrequencyMatrix> InspectionFrequencyMatrices => Set<InspectionFrequencyMatrix>();
    public DbSet<RiskCalculation> RiskCalculations => Set<RiskCalculation>();
    public DbSet<RiskScale> RiskScales => Set<RiskScale>();
    public DbSet<RiskScaleLevel> RiskScaleLevels => Set<RiskScaleLevel>();
    public DbSet<FoodSubcategoryHazard> FoodSubcategoryHazards => Set<FoodSubcategoryHazard>();
    public DbSet<RiskRuleVersion> RiskRuleVersions => Set<RiskRuleVersion>();
    public DbSet<EvaluationTemplate> EvaluationTemplates => Set<EvaluationTemplate>();
    public DbSet<EvaluationTemplateItem> EvaluationTemplateItems => Set<EvaluationTemplateItem>();
    public DbSet<EvaluationResponseOption> EvaluationResponseOptions => Set<EvaluationResponseOption>();
    public DbSet<EvaluationGuidanceCriterion> EvaluationGuidanceCriteria => Set<EvaluationGuidanceCriterion>();
    public DbSet<EvaluationQualificationRule> EvaluationQualificationRules => Set<EvaluationQualificationRule>();
    public DbSet<EvaluationImportBatch> EvaluationImportBatches => Set<EvaluationImportBatch>();
    public DbSet<EvaluationImportRow> EvaluationImportRows => Set<EvaluationImportRow>();
    public DbSet<BpmRequest> BpmRequests => Set<BpmRequest>();
    public DbSet<InspectionCase> InspectionCases => Set<InspectionCase>();
    public DbSet<CaseStateHistory> CaseStateHistories => Set<CaseStateHistory>();
    public DbSet<HealthAlert> HealthAlerts => Set<HealthAlert>();
    public DbSet<Complaint> Complaints => Set<Complaint>();

    public const int PublishedRuleFactorCount = 6;

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnforceRiskInvariants();
        EnforceTemplateInvariants();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnforceRiskInvariants();
        EnforceTemplateInvariants();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void EnforceRiskInvariants()
    {
        ChangeTracker.DetectChanges();
        if (ChangeTracker.Entries<RiskCalculation>().Any(entry =>
                entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException(
                "Un cálculo de riesgo registrado es inmutable y no admite modificación ni eliminación.");
        }

        foreach (var ruleVersionId in PendingPublishedRuleVersionIds())
        {
            var factors = ActiveFactorsOf(ruleVersionId);
            if (factors.Count != PublishedRuleFactorCount)
            {
                throw new InvalidOperationException(
                    $"Una versión publicada de reglas de riesgo requiere {PublishedRuleFactorCount} factores activos.");
            }

            if (factors.Sum(factor => factor.Weight) != 1m)
            {
                throw new InvalidOperationException(
                    "Los pesos de los factores de una versión publicada deben sumar exactamente 1.");
            }
        }
    }

    private HashSet<int> PendingPublishedRuleVersionIds()
    {
        var versions = ChangeTracker.Entries<RiskRuleVersion>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
            .Select(entry => entry.Entity)
            .ToList();
        var candidates = versions.Where(version => version.IsPublished).Select(version => version.Id).ToHashSet();
        foreach (var factorVersionId in ChangeTracker.Entries<StructuralRiskFactor>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(entry => entry.Entity.RuleVersionId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct())
        {
            var known = versions.SingleOrDefault(version => version.Id == factorVersionId) ??
                RiskRuleVersions.SingleOrDefault(version => version.Id == factorVersionId);
            if (known is { IsPublished: true }) candidates.Add(factorVersionId);
        }

        return candidates;
    }

    private List<StructuralRiskFactor> ActiveFactorsOf(int ruleVersionId)
    {
        var removed = ChangeTracker.Entries<StructuralRiskFactor>()
            .Where(entry => entry.State == EntityState.Deleted)
            .Select(entry => entry.Entity)
            .ToHashSet();
        var stored = StructuralRiskFactors.Where(factor => factor.RuleVersionId == ruleVersionId).ToList();
        var added = ChangeTracker.Entries<StructuralRiskFactor>()
            .Where(entry => entry.State == EntityState.Added && entry.Entity.RuleVersionId == ruleVersionId)
            .Select(entry => entry.Entity);
        return stored.Concat(added)
            .Distinct()
            .Where(factor => factor.IsActive && factor.RuleVersionId == ruleVersionId && !removed.Contains(factor))
            .ToList();
    }

    /// <summary>
    /// Una versión publicada de plantilla es inmutable: no admite cambios en su cabecera ni altas,
    /// modificaciones o bajas de ítems, opciones, criterios o reglas de calificación. La misma
    /// invariante se duplica en PostgreSQL con el disparador <c>tr_plantilla_publicada_inmutable</c>.
    /// </summary>
    private void EnforceTemplateInvariants()
    {
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries<EvaluationTemplate>()
            .Where(entry => entry.State is EntityState.Modified or EntityState.Deleted))
        {
            var original = entry.Property(template => template.Status).OriginalValue;
            if (original != EvaluationTemplateStatuses.Draft) throw ImmutableTemplate();
        }

        var touched = TouchedTemplateIds<EvaluationTemplateItem>(item => item.TemplateId);
        touched.UnionWith(TouchedTemplateIds<EvaluationResponseOption>(option => option.TemplateId));
        touched.UnionWith(TouchedTemplateIds<EvaluationQualificationRule>(rule => rule.TemplateId));
        var criteriaItemIds = ChangeTracker.Entries<EvaluationGuidanceCriterion>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(entry => entry.Entity.ItemId)
            .ToHashSet();
        if (criteriaItemIds.Count > 0)
        {
            touched.UnionWith(EvaluationTemplateItems
                .Where(item => criteriaItemIds.Contains(item.Id))
                .Select(item => item.TemplateId));
        }

        if (touched.Count == 0) return;
        if (EvaluationTemplates.Any(template =>
                touched.Contains(template.Id) && template.Status != EvaluationTemplateStatuses.Draft))
        {
            throw ImmutableTemplate();
        }
    }

    private HashSet<int> TouchedTemplateIds<TEntity>(Func<TEntity, int> templateId) where TEntity : class =>
        ChangeTracker.Entries<TEntity>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(entry => templateId(entry.Entity))
            .ToHashSet();

    private static InvalidOperationException ImmutableTemplate() =>
        new("Una versión publicada de plantilla de evaluación es inmutable.");

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshToken");
            entity.HasKey(token => token.Id);
            entity.HasIndex(token => token.TokenHash).IsUnique();
            entity.Property(token => token.TokenHash).HasMaxLength(64);
            entity.Property(token => token.ReplacedByHash).HasMaxLength(64);
        });
        builder.Entity<PasswordRecoveryCode>(entity =>
        {
            entity.ToTable("PasswordRecoveryCode");
            entity.HasKey(code => code.Id);
            entity.HasIndex(code => new { code.UserId, code.ExpiresAt });
            entity.Property(code => code.CodeHash).HasMaxLength(64);
        });
        builder.Entity<Company>(entity =>
        {
            entity.ToTable("Empresa");
            entity.HasKey(company => company.Id);
            entity.HasIndex(company => company.Rnc).IsUnique();
            entity.Property(company => company.LegalName).HasColumnName("razon_social").HasMaxLength(200);
            entity.Property(company => company.Rnc).HasColumnName("rnc").HasMaxLength(20);
            entity.Property(company => company.TradeName).HasColumnName("nombre_comercial").HasMaxLength(200);
            entity.Property(company => company.IsActive).HasColumnName("activo");
        });
        builder.Entity<CompanyUser>(entity =>
        {
            entity.ToTable("Empresa_Usuario");
            entity.HasKey(link => new { link.CompanyId, link.UserId });
            entity.Property(link => link.CompanyId).HasColumnName("empresa_id");
            entity.Property(link => link.UserId).HasColumnName("usuario_id");
            entity.HasOne<Company>().WithMany().HasForeignKey(link => link.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(link => link.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<CompanyRepresentative>(entity =>
        {
            entity.ToTable("Representante_Empresa");
            entity.HasKey(representative => representative.Id);
            entity.HasIndex(representative => new { representative.CompanyId, representative.DocumentNumber }).IsUnique();
            entity.Property(representative => representative.CompanyId).HasColumnName("empresa_id");
            entity.Property(representative => representative.FullName).HasColumnName("nombre_completo").HasMaxLength(200);
            entity.Property(representative => representative.DocumentNumber).HasColumnName("documento").HasMaxLength(30);
            entity.Property(representative => representative.Email).HasColumnName("correo").HasMaxLength(254);
            entity.Property(representative => representative.PhoneNumber).HasColumnName("telefono").HasMaxLength(30);
            entity.Property(representative => representative.IsActive).HasColumnName("vigente");
            entity.HasOne<Company>().WithMany().HasForeignKey(representative => representative.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<RiskLevel>(entity =>
        {
            entity.ToTable("Nivel_Riesgo");
            entity.HasKey(level => level.Id);
            entity.HasIndex(level => level.Name).IsUnique();
            entity.HasIndex(level => level.Points).IsUnique();
            entity.Property(level => level.Name).HasColumnName("nombre").HasMaxLength(50);
            entity.Property(level => level.Points).HasColumnName("puntos");
        });
        ConfigureRiskCatalogs(builder);
        ConfigureEvaluationTemplates(builder);
        ConfigureWorkflow(builder);
    }

    private static void ConfigureWorkflow(ModelBuilder builder)
    {
        builder.Entity<BpmRequest>(entity =>
        {
            entity.ToTable("Solicitud_BPM");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.CompanyId, item.Status, item.CreatedAt });
            entity.Property(item => item.CompanyId).HasColumnName("empresa_id");
            entity.Property(item => item.EstablishmentType).HasColumnName("tipo_establecimiento").HasMaxLength(160);
            entity.Property(item => item.Reason).HasColumnName("motivo").HasMaxLength(500);
            entity.Property(item => item.Observations).HasColumnName("observaciones").HasMaxLength(4000);
            entity.Property(item => item.Status).HasColumnName("estado").HasMaxLength(30);
            entity.Property(item => item.CreatedBy).HasColumnName("creado_por");
            entity.Property(item => item.CreatedAt).HasColumnName("fecha_creacion");
            entity.Property(item => item.UpdatedAt).HasColumnName("fecha_actualizacion");
            entity.Property(item => item.SubmittedAt).HasColumnName("fecha_envio");
            entity.Property(item => item.VersionToken).HasColumnName("version_token").IsConcurrencyToken();
            entity.HasOne<Company>().WithMany().HasForeignKey(item => item.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<InspectionCase>(entity =>
        {
            entity.ToTable("Caso");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.SourceType, item.SourceReferenceId }).IsUnique();
            entity.HasIndex(item => new { item.Status, item.Priority, item.CreatedAt });
            entity.Property(item => item.CompanyId).HasColumnName("empresa_id");
            entity.Property(item => item.SourceType).HasColumnName("tipo_origen").HasMaxLength(30);
            entity.Property(item => item.SourceReferenceId).HasColumnName("origen_id");
            entity.Property(item => item.Priority).HasColumnName("prioridad").HasMaxLength(20);
            entity.Property(item => item.Status).HasColumnName("estado").HasMaxLength(30);
            entity.Property(item => item.CreatedAt).HasColumnName("fecha_creacion");
            entity.Property(item => item.CreatedBy).HasColumnName("creado_por");
            entity.HasOne<Company>().WithMany().HasForeignKey(item => item.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<CaseStateHistory>(entity =>
        {
            entity.ToTable("Caso_Estado_Historial");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.CaseId, item.ChangedAt });
            entity.Property(item => item.CaseId).HasColumnName("caso_id");
            entity.Property(item => item.PreviousStatus).HasColumnName("estado_anterior").HasMaxLength(30);
            entity.Property(item => item.NewStatus).HasColumnName("estado_nuevo").HasMaxLength(30);
            entity.Property(item => item.Reason).HasColumnName("motivo").HasMaxLength(1000);
            entity.Property(item => item.ChangedAt).HasColumnName("fecha_cambio");
            entity.Property(item => item.ChangedBy).HasColumnName("cambiado_por");
            entity.HasOne<InspectionCase>().WithMany().HasForeignKey(item => item.CaseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ChangedBy).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<HealthAlert>(entity =>
        {
            entity.ToTable("Alerta_LAPCH"); entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.AlertNumber).IsUnique();
            entity.HasIndex(item => new { item.Status, item.ReceivedAt });
            entity.Property(item => item.AlertNumber).HasColumnName("numero_alerta").HasMaxLength(80);
            entity.Property(item => item.ReceivedAt).HasColumnName("fecha_recepcion");
            entity.Property(item => item.Product).HasColumnName("producto").HasMaxLength(250);
            entity.Property(item => item.CompanyId).HasColumnName("empresa_id");
            entity.Property(item => item.Description).HasColumnName("descripcion").HasMaxLength(4000);
            entity.Property(item => item.Status).HasColumnName("resultado").HasMaxLength(30);
            entity.Property(item => item.DecisionReason).HasColumnName("motivo_decision").HasMaxLength(1000);
            entity.Property(item => item.DecidedAt).HasColumnName("fecha_decision");
            entity.Property(item => item.CreatedBy).HasColumnName("creado_por");
            entity.HasOne<Company>().WithMany().HasForeignKey(item => item.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<Complaint>(entity =>
        {
            entity.ToTable("Denuncia"); entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.Status, item.ReceivedAt });
            entity.Property(item => item.ComplaintType).HasColumnName("tipo_denuncia").HasMaxLength(120);
            entity.Property(item => item.ReceivedAt).HasColumnName("fecha_recepcion");
            entity.Property(item => item.Complainant).HasColumnName("denunciante").HasMaxLength(250);
            entity.Property(item => item.Description).HasColumnName("descripcion").HasMaxLength(4000);
            entity.Property(item => item.CompanyId).HasColumnName("empresa_id");
            entity.Property(item => item.Status).HasColumnName("resultado").HasMaxLength(30);
            entity.Property(item => item.DecisionReason).HasColumnName("motivo_decision").HasMaxLength(1000);
            entity.Property(item => item.DecidedAt).HasColumnName("fecha_decision");
            entity.Property(item => item.CreatedBy).HasColumnName("creado_por");
            entity.HasOne<Company>().WithMany().HasForeignKey(item => item.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureEvaluationTemplates(ModelBuilder builder)
    {
        builder.Entity<EvaluationTemplate>(entity =>
        {
            entity.ToTable("Plantilla_Evaluacion");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.FamilyId, item.Version }).IsUnique();
            entity.Property(item => item.FamilyId).HasColumnName("familia_id");
            entity.Property(item => item.Name).HasColumnName("nombre").HasMaxLength(160);
            entity.Property(item => item.Version).HasColumnName("version");
            entity.Property(item => item.Status).HasColumnName("estado").HasMaxLength(20);
            entity.Property(item => item.CreatedAt).HasColumnName("fecha_creacion");
            entity.Property(item => item.PublishedAt).HasColumnName("fecha_publicacion");
        });
        builder.Entity<EvaluationTemplateItem>(entity =>
        {
            entity.ToTable("Plantilla_Evaluacion_Item");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TemplateId, item.Code }).IsUnique();
            entity.HasIndex(item => new { item.TemplateId, item.ParentId, item.Order });
            entity.Property(item => item.TemplateId).HasColumnName("plantilla_id");
            entity.Property(item => item.ParentId).HasColumnName("padre_id");
            entity.Property(item => item.Code).HasColumnName("codigo").HasMaxLength(50);
            entity.Property(item => item.Description).HasColumnName("descripcion").HasColumnType("text");
            entity.Property(item => item.ItemType).HasColumnName("tipo_item").HasMaxLength(20);
            entity.Property(item => item.Order).HasColumnName("orden");
            entity.Property(item => item.Weight).HasColumnName("peso").HasPrecision(9, 3);
            entity.Property(item => item.IsRequired).HasColumnName("requerido");
            entity.Property(item => item.IsCritical).HasColumnName("critico");
            entity.Property(item => item.AllowsNotApplicable).HasColumnName("permite_no_aplica");
            entity.Property(item => item.ResponseType).HasColumnName("tipo_respuesta").HasMaxLength(30);
            entity.Property(item => item.RulesJson).HasColumnName("reglas").HasColumnType("jsonb");
            entity.Property(item => item.ScoreConfigurationJson).HasColumnName("configuracion_puntaje").HasColumnType("jsonb");
            entity.Property(item => item.IsActive).HasColumnName("activo");
            entity.Property(item => item.VersionToken).HasColumnName("version_token").IsConcurrencyToken();
            entity.HasOne<EvaluationTemplate>().WithMany().HasForeignKey(item => item.TemplateId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<EvaluationTemplateItem>().WithMany().HasForeignKey(item => item.ParentId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<EvaluationResponseOption>(entity =>
        {
            entity.ToTable("Plantilla_Evaluacion_Opcion", table => table.HasCheckConstraint(
                "CK_Opcion_Evaluable",
                "(valor IS NULL AND NOT cuenta_denominador) OR (valor IS NOT NULL AND cuenta_denominador AND valor BETWEEN 0 AND 1)"));
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TemplateId, item.Code }).IsUnique();
            entity.Property(item => item.TemplateId).HasColumnName("plantilla_id");
            entity.Property(item => item.Code).HasColumnName("codigo").HasMaxLength(5);
            entity.Property(item => item.Name).HasColumnName("nombre").HasColumnType("text");
            entity.Property(item => item.Value).HasColumnName("valor").HasPrecision(4, 2);
            entity.Property(item => item.CountsTowardDenominator).HasColumnName("cuenta_denominador");
            entity.Property(item => item.Order).HasColumnName("orden");
            entity.HasOne<EvaluationTemplate>().WithMany().HasForeignKey(item => item.TemplateId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<EvaluationGuidanceCriterion>(entity =>
        {
            entity.ToTable("Plantilla_Evaluacion_Criterio", table => table.HasCheckConstraint(
                "CK_Criterio_Criticidad",
                "criticidad IS NULL OR criticidad IN ('CRITICAL','MAJOR','MINOR')"));
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.ItemId, item.Code }).IsUnique();
            entity.Property(item => item.ItemId).HasColumnName("item_id");
            entity.Property(item => item.Code).HasColumnName("codigo").HasMaxLength(10);
            entity.Property(item => item.Description).HasColumnName("descripcion").HasColumnType("text");
            entity.Property(item => item.Criticality).HasColumnName("criticidad").HasMaxLength(10);
            entity.Property(item => item.SourceSheet).HasColumnName("hoja_origen").HasMaxLength(60);
            entity.Property(item => item.SourceCell).HasColumnName("celda_origen").HasMaxLength(10);
            entity.Property(item => item.Order).HasColumnName("orden");
            entity.HasOne<EvaluationTemplateItem>().WithMany().HasForeignKey(item => item.ItemId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<EvaluationQualificationRule>(entity =>
        {
            entity.ToTable("Plantilla_Evaluacion_Regla", table => table.HasCheckConstraint(
                "CK_Regla_Calificacion",
                "porcentaje_min IS NOT NULL OR porcentaje_max IS NOT NULL"));
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TemplateId, item.Code }).IsUnique();
            entity.Property(item => item.TemplateId).HasColumnName("plantilla_id");
            entity.Property(item => item.Code).HasColumnName("codigo").HasMaxLength(10);
            entity.Property(item => item.Description).HasColumnName("descripcion").HasColumnType("text");
            entity.Property(item => item.Classification).HasColumnName("clasificacion").HasColumnType("text");
            entity.Property(item => item.Action).HasColumnName("accion").HasColumnType("text");
            entity.Property(item => item.MinPercentage).HasColumnName("porcentaje_min").HasPrecision(5, 2);
            entity.Property(item => item.MinIncluded).HasColumnName("minimo_incluido");
            entity.Property(item => item.MaxPercentage).HasColumnName("porcentaje_max").HasPrecision(5, 2);
            entity.Property(item => item.MaxIncluded).HasColumnName("maximo_incluido");
            entity.Property(item => item.Order).HasColumnName("orden");
            entity.HasOne<EvaluationTemplate>().WithMany().HasForeignKey(item => item.TemplateId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<EvaluationImportBatch>(entity =>
        {
            entity.ToTable("Plantilla_Importacion_Lote", table => table.HasCheckConstraint(
                "CK_Lote_Estado", "estado IN ('PENDIENTE','PROMOVIDO','RECHAZADO')"));
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.FileName, item.SheetName, item.ContentHash }).IsUnique();
            entity.Property(item => item.FileName).HasColumnName("archivo").HasMaxLength(260);
            entity.Property(item => item.SheetName).HasColumnName("hoja").HasMaxLength(60);
            entity.Property(item => item.ContentHash).HasColumnName("hash_contenido").HasMaxLength(64);
            entity.Property(item => item.Status).HasColumnName("estado").HasMaxLength(15);
            entity.Property(item => item.CreatedAt).HasColumnName("fecha_creacion");
            entity.Property(item => item.CreatedBy).HasColumnName("creado_por");
            entity.Property(item => item.TemplateId).HasColumnName("plantilla_id");
            entity.Property(item => item.ErrorDetail).HasColumnName("detalle_error").HasColumnType("text");
            entity.HasOne<EvaluationTemplate>().WithMany().HasForeignKey(item => item.TemplateId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<EvaluationImportRow>(entity =>
        {
            entity.ToTable("Plantilla_Importacion_Fila", table => table.HasCheckConstraint(
                "CK_Fila_Estado", "estado IN ('PENDIENTE','PROMOVIDO','RECHAZADO')"));
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.BatchId, item.Code }).IsUnique();
            entity.Property(item => item.BatchId).HasColumnName("lote_id");
            entity.Property(item => item.SourceCell).HasColumnName("celda_origen").HasMaxLength(10);
            entity.Property(item => item.Code).HasColumnName("codigo").HasMaxLength(50);
            entity.Property(item => item.ParentCode).HasColumnName("codigo_padre").HasMaxLength(50);
            entity.Property(item => item.Description).HasColumnName("descripcion").HasColumnType("text");
            entity.Property(item => item.ItemType).HasColumnName("tipo_item").HasMaxLength(20);
            entity.Property(item => item.Order).HasColumnName("orden");
            entity.Property(item => item.Status).HasColumnName("estado").HasMaxLength(15);
            entity.Property(item => item.ErrorDetail).HasColumnName("detalle_error").HasColumnType("text");
            entity.HasOne<EvaluationImportBatch>().WithMany().HasForeignKey(item => item.BatchId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureRiskCatalogs(ModelBuilder builder)
    {
        builder.Entity<RiskScale>(entity =>
        {
            entity.ToTable("Escala_Riesgo", table => table.HasCheckConstraint("CK_Escala_Vigencia", "vigente_hasta IS NULL OR vigente_hasta > vigente_desde"));
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.Code, item.Version }).IsUnique();
            entity.Property(item => item.Code).HasColumnName("codigo").HasMaxLength(30);
            entity.Property(item => item.Name).HasColumnName("nombre").HasColumnType("text");
            entity.Property(item => item.Version).HasColumnName("version");
            entity.Property(item => item.EffectiveFrom).HasColumnName("vigente_desde");
            entity.Property(item => item.EffectiveTo).HasColumnName("vigente_hasta");
            entity.Property(item => item.IsActive).HasColumnName("activo");
            entity.HasMany(item => item.Levels).WithOne().HasForeignKey(item => item.ScaleId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<RiskScaleLevel>(entity =>
        {
            entity.ToTable("Escala_Riesgo_Nivel", table => table.HasCheckConstraint("CK_Nivel_Valido", "codigo IN ('LOW','MEDIUM','HIGH') AND puntaje > 0 AND orden BETWEEN 1 AND 3"));
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.ScaleId, item.Code }).IsUnique();
            entity.HasIndex(item => new { item.ScaleId, item.Rank }).IsUnique();
            entity.Property(item => item.ScaleId).HasColumnName("escala_id");
            entity.Property(item => item.Code).HasColumnName("codigo").HasMaxLength(10);
            entity.Property(item => item.Name).HasColumnName("nombre").HasColumnType("text");
            entity.Property(item => item.Score).HasColumnName("puntaje").HasPrecision(8, 3);
            entity.Property(item => item.Rank).HasColumnName("orden");
        });
        builder.Entity<FoodSubcategoryHazard>(entity =>
        {
            entity.ToTable("Subcategoria_Alimento_Peligro", table => table.HasCheckConstraint("CK_Peligro_Valido", "tipo_peligro IN ('MICROBIOLOGICAL','CHEMICAL') AND (puntaje_propio IS NULL OR puntaje_propio > 0)"));
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.SubcategoryId, item.HazardType }).IsUnique();
            entity.Property(item => item.SubcategoryId).HasColumnName("subcategoria_id");
            entity.Property(item => item.HazardType).HasColumnName("tipo_peligro").HasMaxLength(20);
            entity.Property(item => item.LevelId).HasColumnName("nivel_escala_id");
            entity.Property(item => item.ScoreOverride).HasColumnName("puntaje_propio").HasPrecision(8, 3);
            entity.HasOne<FoodSubcategory>().WithMany().HasForeignKey(item => item.SubcategoryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<RiskScaleLevel>().WithMany().HasForeignKey(item => item.LevelId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<RiskRuleVersion>(entity =>
        {
            entity.ToTable("Version_Regla_Riesgo", table => table.HasCheckConstraint("CK_Regla_Valida", "metodo_agregacion = 'MAX' AND (vigente_hasta IS NULL OR vigente_hasta > vigente_desde)"));
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.Version).IsUnique();
            entity.Property(item => item.Version).HasColumnName("version");
            entity.Property(item => item.EffectiveFrom).HasColumnName("vigente_desde");
            entity.Property(item => item.EffectiveTo).HasColumnName("vigente_hasta");
            entity.Property(item => item.IsActive).HasColumnName("activo");
            entity.Property(item => item.IsPublished).HasColumnName("publicado");
            entity.Property(item => item.AggregationMethod).HasColumnName("metodo_agregacion").HasMaxLength(10);
            entity.Property(item => item.ProductScaleId).HasColumnName("escala_producto_id");
            entity.Property(item => item.FrequencyScaleId).HasColumnName("escala_frecuencia_id");
            entity.HasOne<RiskScale>().WithMany().HasForeignKey(item => item.ProductScaleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<RiskScale>().WithMany().HasForeignKey(item => item.FrequencyScaleId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<FoodCategory>(entity =>
        {
            entity.ToTable("Categoria_Alimento");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.Name).IsUnique();
            entity.Property(item => item.Name).HasColumnName("nombre").HasColumnType("text");
        });
        builder.Entity<FoodSubcategory>(entity =>
        {
            entity.ToTable("Subcategoria_Alimento");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.CategoryId, item.Name }).IsUnique();
            entity.Property(item => item.CategoryId).HasColumnName("categoria_id");
            entity.Property(item => item.Name).HasColumnName("nombre").HasColumnType("text");
            entity.Property(item => item.MicrobiologicalRiskLevelId).HasColumnName("riesgo_micro_id");
            entity.Property(item => item.MicrobiologicalScore).HasColumnName("puntaje_micro").HasPrecision(8, 3);
            entity.Property(item => item.ChemicalRiskLevelId).HasColumnName("riesgo_quimi_id");
            entity.Property(item => item.ChemicalScore).HasColumnName("puntaje_quimico").HasPrecision(8, 3);
            entity.Property(item => item.TotalRiskLevelId).HasColumnName("nivel_riesgo_total");
            entity.Property(item => item.TotalScore).HasColumnName("puntaje_total").HasPrecision(8, 3);
            entity.HasOne<FoodCategory>().WithMany().HasForeignKey(item => item.CategoryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<RiskLevel>().WithMany().HasForeignKey(item => item.MicrobiologicalRiskLevelId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<RiskLevel>().WithMany().HasForeignKey(item => item.ChemicalRiskLevelId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<RiskLevel>().WithMany().HasForeignKey(item => item.TotalRiskLevelId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<CompanyFoodSubcategory>(entity =>
        {
            entity.ToTable("Establecimiento_Subcategoria");
            entity.HasKey(item => new { item.CompanyId, item.SubcategoryId });
            entity.Property(item => item.CompanyId).HasColumnName("empresa_id");
            entity.Property(item => item.SubcategoryId).HasColumnName("subcategoria_id");
            entity.HasOne<Company>().WithMany().HasForeignKey(item => item.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FoodSubcategory>().WithMany().HasForeignKey(item => item.SubcategoryId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<StructuralRiskFactor>(entity =>
        {
            entity.ToTable("Factor_Riesgo_Establecimiento");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.RuleVersionId, item.Code }).IsUnique();
            entity.HasOne<RiskRuleVersion>().WithMany().HasForeignKey(item => item.RuleVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(item => item.RuleVersionId).HasColumnName("version_regla_id");
            entity.Property(item => item.IsActive).HasColumnName("activo");
            entity.Property(item => item.Code).HasColumnName("codigo").HasMaxLength(30);
            entity.Property(item => item.Name).HasColumnName("nombre").HasMaxLength(160);
            entity.Property(item => item.Weight).HasColumnName("peso").HasPrecision(8, 4);
            entity.Property(item => item.Order).HasColumnName("orden");
            entity.HasMany(item => item.Options).WithOne().HasForeignKey(option => option.FactorId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<StructuralRiskOption>(entity =>
        {
            entity.ToTable("Factor_Riesgo_Opcion");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.FactorId, item.Order }).IsUnique();
            entity.Property(item => item.FactorId).HasColumnName("factor_id");
            entity.Property(item => item.Description).HasColumnName("descripcion").HasMaxLength(200);
            entity.Property(item => item.Score).HasColumnName("puntaje").HasPrecision(8, 3);
            entity.Property(item => item.Order).HasColumnName("orden");
        });
        builder.Entity<CompanyRiskFactorValue>(entity =>
        {
            entity.ToTable("Establecimiento_Factor_Valor");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.CompanyId, item.FactorId, item.IsCurrent });
            entity.Property(item => item.CompanyId).HasColumnName("empresa_id");
            entity.Property(item => item.FactorId).HasColumnName("factor_id");
            entity.Property(item => item.OptionId).HasColumnName("opcion_id");
            entity.Property(item => item.RegisteredAt).HasColumnName("fecha_registro");
            entity.Property(item => item.IsCurrent).HasColumnName("vigente");
            entity.Property(item => item.RegisteredBy).HasColumnName("registrado_por");
            entity.HasOne<Company>().WithMany().HasForeignKey(item => item.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StructuralRiskFactor>().WithMany().HasForeignKey(item => item.FactorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StructuralRiskOption>().WithMany().HasForeignKey(item => item.OptionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.RegisteredBy).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<InspectionFrequencyMatrix>(entity =>
        {
            entity.ToTable("Matriz_Frecuencia_Inspeccion");
            entity.HasKey(item => item.Id);
            entity.HasOne<RiskRuleVersion>().WithMany().HasForeignKey(item => item.RuleVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<RiskScaleLevel>().WithMany().HasForeignKey(item => item.ScaleLevelId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(item => item.RuleVersionId).HasColumnName("version_regla_id");
            entity.Property(item => item.ScaleLevelId).HasColumnName("nivel_escala_id");
            entity.Property(item => item.RiskMin).HasColumnName("riesgo_min").HasPrecision(8, 3);
            entity.Property(item => item.MinimumIncluded).HasColumnName("riesgo_min_incluido");
            entity.Property(item => item.RiskMax).HasColumnName("riesgo_max").HasPrecision(8, 3);
            entity.Property(item => item.RiskLevelId).HasColumnName("nivel_riesgo_id");
            entity.Property(item => item.FrequencyMonths).HasColumnName("frecuencia_meses");
            entity.HasOne<RiskLevel>().WithMany().HasForeignKey(item => item.RiskLevelId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<RiskCalculation>(entity =>
        {
            entity.ToTable("Calculo_Riesgo");
            entity.HasKey(item => item.Id);
            entity.HasOne<RiskRuleVersion>().WithMany().HasForeignKey(item => item.RuleVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(item => new { item.CompanyId, item.CalculatedAt });
            entity.Property(item => item.RuleVersionId).HasColumnName("version_regla_id");
            entity.Property(item => item.CompanyId).HasColumnName("empresa_id");
            entity.Property(item => item.CalculatedAt).HasColumnName("fecha_calculo");
            entity.Property(item => item.ProductRisk).HasColumnName("riesgo_producto_rd").HasPrecision(8, 3);
            entity.Property(item => item.EstablishmentRisk).HasColumnName("riesgo_establecimiento_re").HasPrecision(8, 3);
            entity.Property(item => item.TotalRisk).HasColumnName("riesgo_total_rt").HasPrecision(8, 3);
            entity.Property(item => item.RiskLevelId).HasColumnName("nivel_riesgo_id");
            entity.Property(item => item.InspectionFrequencyMatrixId).HasColumnName("matriz_frecuencia_id");
            entity.Property(item => item.FactorDetailsJson).HasColumnName("detalle_factores").HasColumnType("jsonb");
            entity.Property(item => item.GeneratedBy).HasColumnName("generado_por");
            entity.HasOne<Company>().WithMany().HasForeignKey(item => item.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<RiskLevel>().WithMany().HasForeignKey(item => item.RiskLevelId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<InspectionFrequencyMatrix>().WithMany().HasForeignKey(item => item.InspectionFrequencyMatrixId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.GeneratedBy).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
