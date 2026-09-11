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

    public DbSet<CompanyHistoryEntry> CompanyHistoryEntries => Set<CompanyHistoryEntry>();

    public DbSet<UserRegistrationDocument> UserRegistrationDocuments => Set<UserRegistrationDocument>();

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
    public DbSet<ActiveEvaluationTemplate> ActiveEvaluationTemplates => Set<ActiveEvaluationTemplate>();
    public DbSet<EvaluationImportBatch> EvaluationImportBatches => Set<EvaluationImportBatch>();
    public DbSet<EvaluationImportRow> EvaluationImportRows => Set<EvaluationImportRow>();
    public DbSet<BpmRequest> BpmRequests => Set<BpmRequest>();
    public DbSet<BpmRequestDocument> BpmRequestDocuments => Set<BpmRequestDocument>();
    public DbSet<InspectionCase> InspectionCases => Set<InspectionCase>();
    public DbSet<CaseStateHistory> CaseStateHistories => Set<CaseStateHistory>();
    public DbSet<CaseAssignment> CaseAssignments => Set<CaseAssignment>();
    public DbSet<CaseSchedule> CaseSchedules => Set<CaseSchedule>();
    public DbSet<EvaluationInstance> EvaluationInstances => Set<EvaluationInstance>();
    public DbSet<EvaluationResponse> EvaluationResponses => Set<EvaluationResponse>();
    public DbSet<EvaluationResult> EvaluationResults => Set<EvaluationResult>();
    public DbSet<EvaluationNonConformity> EvaluationNonConformities => Set<EvaluationNonConformity>();
    public DbSet<EvaluationEvidence> EvaluationEvidences => Set<EvaluationEvidence>();
    public DbSet<EvaluationReport> EvaluationReports => Set<EvaluationReport>();
    public DbSet<EvaluationReportReview> EvaluationReportReviews => Set<EvaluationReportReview>();
    public DbSet<EvaluationOfficialReport> EvaluationOfficialReports => Set<EvaluationOfficialReport>();
    public DbSet<CaseClosure> CaseClosures => Set<CaseClosure>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<HealthAlert> HealthAlerts => Set<HealthAlert>();
    public DbSet<Complaint> Complaints => Set<Complaint>();
    public DbSet<InstitutionalScheduling> InstitutionalSchedulings => Set<InstitutionalScheduling>();

    public const int PublishedRuleFactorCount = 6;

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnforceRiskInvariants();
        EnforceTemplateInvariants();
        EnforceCompanyHistoryInvariants();
        EnforceCaseAssignmentHistoryInvariants();
        EnforceEvaluationInstanceInvariants();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnforceRiskInvariants();
        EnforceTemplateInvariants();
        EnforceCompanyHistoryInvariants();
        EnforceCaseAssignmentHistoryInvariants();
        EnforceEvaluationInstanceInvariants();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// El historial de cambios de una empresa es append-only: no admite modificación ni eliminación
    /// de filas existentes. La misma invariante se duplica en PostgreSQL con el disparador
    /// <c>tr_empresa_historial_inmutable</c>.
    /// </summary>
    private void EnforceCompanyHistoryInvariants()
    {
        ChangeTracker.DetectChanges();
        if (ChangeTracker.Entries<CompanyHistoryEntry>().Any(entry =>
                entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException(
                "El historial de una empresa es append-only y no admite modificación ni eliminación.");
        }
    }

    private void EnforceCaseAssignmentHistoryInvariants()
    {
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries<CaseAssignment>())
        {
            if (entry.State == EntityState.Deleted) throw ImmutableCaseAssignment();
            if (entry.State != EntityState.Modified) continue;
            var allowedRetirement = entry.Property(value => value.IsCurrent).OriginalValue &&
                                    !entry.Property(value => value.IsCurrent).CurrentValue &&
                                    entry.Properties.Where(property => property.IsModified)
                                        .All(property => property.Metadata.Name == nameof(CaseAssignment.IsCurrent));
            if (!allowedRetirement) throw ImmutableCaseAssignment();
        }
    }

    private static InvalidOperationException ImmutableCaseAssignment() =>
        new("El historial de asignaciones solo permite retirar la vigencia; no admite otros cambios ni eliminaciones.");

    /// <summary>
    /// Una evaluación enviada (<see cref="EvaluationInstanceStatuses.Submitted"/>) es inmutable: ni la
    /// cabecera de la instancia ni sus respuestas admiten cambios posteriores. La misma invariante se
    /// duplica en PostgreSQL con el disparador <c>tr_evaluacion_respuesta_bloqueada</c> sobre
    /// <c>Evaluacion_Respuesta</c>. El resultado calculado en el envío (<see cref="EvaluationResult"/> y
    /// sus no conformidades) tampoco admite cambios: es la fotografía con la que se emitirá el informe,
    /// y su equivalente en PostgreSQL es <c>tr_evaluacion_resultado_inmutable</c>.
    /// </summary>
    private void EnforceEvaluationInstanceInvariants()
    {
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries<EvaluationInstance>()
            .Where(entry => entry.State is EntityState.Modified or EntityState.Deleted))
        {
            var original = entry.Property(instance => instance.Status).OriginalValue;
            if (original == EvaluationInstanceStatuses.Submitted) throw ImmutableEvaluationInstance();
        }

        if (ChangeTracker.Entries<EvaluationResult>().Any(entry => entry.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<EvaluationNonConformity>().Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw ImmutableEvaluationResult();
        }

        var touchedInstanceIds = ChangeTracker.Entries<EvaluationResponse>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(entry => entry.Entity.EvaluationInstanceId)
            .ToHashSet();
        if (touchedInstanceIds.Count == 0) return;

        foreach (var responseEntry in ChangeTracker.Entries<EvaluationResponse>()
                     .Where(entry => entry.State is EntityState.Added or EntityState.Modified))
        {
            var response = responseEntry.Entity;
            var instanceTemplateId = ChangeTracker.Entries<EvaluationInstance>()
                .Select(entry => entry.Entity)
                .Where(instance => instance.Id == response.EvaluationInstanceId)
                .Select(instance => (int?)instance.TemplateId)
                .FirstOrDefault() ?? EvaluationInstances
                .Where(instance => instance.Id == response.EvaluationInstanceId)
                .Select(instance => (int?)instance.TemplateId).SingleOrDefault();
            var validItem = ChangeTracker.Entries<EvaluationTemplateItem>()
                .Select(entry => entry.Entity)
                .Any(item => item.Id == response.TemplateItemId && item.TemplateId == instanceTemplateId &&
                             item.IsActive && item.ItemType == EvaluationItemTypes.Question) ||
                EvaluationTemplateItems.Any(item => item.Id == response.TemplateItemId && item.TemplateId == instanceTemplateId &&
                                                    item.IsActive && item.ItemType == EvaluationItemTypes.Question);
            if (!validItem)
                throw new InvalidOperationException("La respuesta no pertenece a una pregunta activa de la plantilla congelada.");
        }

        var submittedIds = ChangeTracker.Entries<EvaluationInstance>()
            .Where(entry => touchedInstanceIds.Contains(entry.Entity.Id) && entry.Entity.Status == EvaluationInstanceStatuses.Submitted)
            .Select(entry => entry.Entity.Id)
            .ToHashSet();
        submittedIds.UnionWith(EvaluationInstances
            .Where(instance => touchedInstanceIds.Contains(instance.Id) && instance.Status == EvaluationInstanceStatuses.Submitted)
            .Select(instance => instance.Id));
        if (submittedIds.Count > 0) throw ImmutableEvaluationInstance();
    }

    private static InvalidOperationException ImmutableEvaluationResult() =>
        new("El resultado de una evaluación es una fotografía inmutable y no admite modificación ni eliminación.");

    private static InvalidOperationException ImmutableEvaluationInstance() =>
        new("Una evaluación enviada es inmutable y no admite modificación de su cabecera ni de sus respuestas.");

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
            entity.Property(company => company.Address).HasColumnName("direccion").HasMaxLength(300);
            entity.Property(company => company.Municipality).HasColumnName("municipio").HasMaxLength(120);
            entity.Property(company => company.Province).HasColumnName("provincia").HasMaxLength(120);
            entity.Property(company => company.PhoneNumber).HasColumnName("telefono").HasMaxLength(30);
            entity.Property(company => company.Email).HasColumnName("correo").HasMaxLength(254);
            entity.Property(company => company.EconomicActivity).HasColumnName("actividad_economica").HasMaxLength(300);
            entity.Property(company => company.IsActive).HasColumnName("activo");
            entity.Property(company => company.VersionToken).HasColumnName("version_token").IsConcurrencyToken();
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
            entity.ToTable("Representante_Empresa", table => table.HasCheckConstraint(
                "CK_Representante_Tipo",
                "tipo_representante IN ('LEGAL','CALIDAD','CONTACTO_PRINCIPAL')"));
            entity.HasKey(representative => representative.Id);
            entity.HasIndex(representative => new { representative.CompanyId, representative.DocumentNumber }).IsUnique();
            entity.HasIndex(representative => new { representative.CompanyId, representative.RepresentativeType })
                .IsUnique()
                .HasFilter("vigente")
                .HasDatabaseName("IX_Representante_Empresa_empresa_id_tipo_representante_vigente");
            entity.Property(representative => representative.CompanyId).HasColumnName("empresa_id");
            entity.Property(representative => representative.FullName).HasColumnName("nombre_completo").HasMaxLength(200);
            entity.Property(representative => representative.DocumentNumber).HasColumnName("documento").HasMaxLength(30);
            entity.Property(representative => representative.Email).HasColumnName("correo").HasMaxLength(254);
            entity.Property(representative => representative.PhoneNumber).HasColumnName("telefono").HasMaxLength(30);
            entity.Property(representative => representative.RepresentativeType).HasColumnName("tipo_representante").HasMaxLength(30);
            entity.Property(representative => representative.IsActive).HasColumnName("vigente");
            entity.HasOne<Company>().WithMany().HasForeignKey(representative => representative.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<CompanyHistoryEntry>(entity =>
        {
            entity.ToTable("Empresa_Historial");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.CompanyId, item.ChangedAt });
            entity.Property(item => item.CompanyId).HasColumnName("empresa_id");
            entity.Property(item => item.ChangedAt).HasColumnName("fecha_cambio");
            entity.Property(item => item.ChangedBy).HasColumnName("cambiado_por");
            entity.Property(item => item.ChangesJson).HasColumnName("cambios").HasColumnType("jsonb");
            entity.HasOne<Company>().WithMany().HasForeignKey(item => item.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ChangedBy).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<UserRegistrationDocument>(entity =>
        {
            entity.ToTable("Documento_Registro_Usuario", table => table.HasCheckConstraint(
                "CK_Documento_Registro_Tipo",
                "tipo_documento IN ('CARTA_AUTORIZACION')"));
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.UserId, item.DocumentType });
            entity.Property(item => item.UserId).HasColumnName("usuario_id");
            entity.Property(item => item.DocumentType).HasColumnName("tipo_documento").HasMaxLength(30);
            entity.Property(item => item.FileName).HasColumnName("nombre_archivo").HasMaxLength(260);
            entity.Property(item => item.MimeType).HasColumnName("tipo_mime").HasMaxLength(120);
            entity.Property(item => item.SizeBytes).HasColumnName("tamano_bytes");
            entity.Property(item => item.Hash).HasColumnName("hash").HasMaxLength(128);
            entity.Property(item => item.StorageReference).HasColumnName("referencia_almacenamiento").HasMaxLength(500);
            entity.Property(item => item.UploadedAt).HasColumnName("fecha_carga");
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
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
        ConfigureEvaluationInstances(builder);
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
        builder.Entity<BpmRequestDocument>(entity =>
        {
            entity.ToTable("Solicitud_BPM_Documento");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.BpmRequestId, item.UploadedAt });
            entity.Property(item => item.BpmRequestId).HasColumnName("solicitud_id");
            entity.Property(item => item.DocumentType).HasColumnName("tipo_documento").HasMaxLength(120);
            entity.Property(item => item.FileName).HasColumnName("nombre_archivo").HasMaxLength(260);
            entity.Property(item => item.MimeType).HasColumnName("tipo_mime").HasMaxLength(120);
            entity.Property(item => item.SizeBytes).HasColumnName("tamano_bytes");
            entity.Property(item => item.Hash).HasColumnName("hash").HasMaxLength(128);
            entity.Property(item => item.StorageReference).HasColumnName("referencia_almacenamiento").HasMaxLength(500);
            entity.Property(item => item.UploadedAt).HasColumnName("fecha_carga");
            entity.HasOne<BpmRequest>().WithMany().HasForeignKey(item => item.BpmRequestId).OnDelete(DeleteBehavior.Restrict);
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
        builder.Entity<CaseAssignment>(entity =>
        {
            entity.ToTable("Caso_Asignacion");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.CaseId)
                .IsUnique()
                .HasFilter("vigente")
                .HasDatabaseName("IX_Caso_Asignacion_caso_id_vigente");
            entity.HasIndex(item => new { item.CaseId, item.AssignedAt });
            entity.Property(item => item.CaseId).HasColumnName("caso_id");
            entity.Property(item => item.TechnicianId).HasColumnName("tecnico_id");
            entity.Property(item => item.IsCurrent).HasColumnName("vigente");
            entity.Property(item => item.AssignedAt).HasColumnName("fecha_asignacion");
            entity.Property(item => item.AssignedBy).HasColumnName("asignado_por");
            entity.Property(item => item.Reason).HasColumnName("motivo").HasMaxLength(1000);
            entity.HasOne<InspectionCase>().WithMany().HasForeignKey(item => item.CaseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.TechnicianId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.AssignedBy).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<CaseSchedule>(entity =>
        {
            entity.ToTable("Caso_Programacion");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.CaseId)
                .IsUnique()
                .HasFilter("vigente")
                .HasDatabaseName("IX_Caso_Programacion_caso_id_vigente");
            entity.HasIndex(item => new { item.TechnicianId, item.IsCurrent })
                .HasDatabaseName("IX_Caso_Programacion_tecnico_id_vigente");
            entity.HasIndex(item => new { item.CaseId, item.CreatedAt });
            entity.Property(item => item.CaseId).HasColumnName("caso_id");
            entity.Property(item => item.TechnicianId).HasColumnName("tecnico_id");
            entity.Property(item => item.ScheduledFor).HasColumnName("fecha_programada");
            entity.Property(item => item.Priority).HasColumnName("prioridad").HasMaxLength(20);
            entity.Property(item => item.Reason).HasColumnName("motivo").HasMaxLength(1000);
            entity.Property(item => item.Observations).HasColumnName("observaciones").HasMaxLength(2000);
            entity.Property(item => item.IsCurrent).HasColumnName("vigente");
            entity.Property(item => item.CreatedAt).HasColumnName("fecha_creacion");
            entity.Property(item => item.ScheduledBy).HasColumnName("programado_por");
            entity.Property(item => item.CancelledAt).HasColumnName("fecha_cancelacion");
            entity.Property(item => item.CancelledBy).HasColumnName("cancelado_por");
            entity.Property(item => item.CancellationReason).HasColumnName("motivo_cancelacion").HasMaxLength(1000);
            entity.HasOne<InspectionCase>().WithMany().HasForeignKey(item => item.CaseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.TechnicianId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ScheduledBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.CancelledBy).OnDelete(DeleteBehavior.Restrict);
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
        builder.Entity<InstitutionalScheduling>(entity =>
        {
            entity.ToTable("Programacion_Institucional"); entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.CompanyId, item.CreatedAt });
            entity.Property(item => item.CompanyId).HasColumnName("empresa_id");
            entity.Property(item => item.Reason).HasColumnName("motivo").HasMaxLength(1000);
            entity.Property(item => item.Observations).HasColumnName("observaciones").HasMaxLength(2000);
            entity.Property(item => item.CreatedAt).HasColumnName("fecha_creacion");
            entity.Property(item => item.CreatedBy).HasColumnName("creado_por");
            entity.HasOne<Company>().WithMany().HasForeignKey(item => item.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureEvaluationInstances(ModelBuilder builder)
    {
        builder.Entity<EvaluationInstance>(entity =>
        {
            entity.ToTable("Evaluacion_Instancia", table =>
            {
                table.HasCheckConstraint("CK_Evaluacion_Instancia_Estado", "estado IN ('IN_PROGRESS','SUBMITTED')");
                table.HasCheckConstraint("CK_Evaluacion_Instancia_Envio", "(estado = 'IN_PROGRESS' AND fecha_envio IS NULL AND enviado_por IS NULL) OR (estado = 'SUBMITTED' AND fecha_envio IS NOT NULL AND enviado_por IS NOT NULL)");
            });
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.CaseId).IsUnique();
            entity.Property(item => item.CaseId).HasColumnName("caso_id");
            entity.Property(item => item.TemplateId).HasColumnName("plantilla_id");
            entity.Property(item => item.TemplateFamilyId).HasColumnName("familia_plantilla_id");
            entity.Property(item => item.RiskRuleVersionId).HasColumnName("version_regla_riesgo_id");
            entity.Property(item => item.Status).HasColumnName("estado").HasMaxLength(20);
            entity.Property(item => item.StartedAt).HasColumnName("fecha_inicio");
            entity.Property(item => item.StartedBy).HasColumnName("iniciado_por");
            entity.Property(item => item.SubmittedAt).HasColumnName("fecha_envio");
            entity.Property(item => item.SubmittedBy).HasColumnName("enviado_por");
            entity.HasOne<InspectionCase>().WithMany().HasForeignKey(item => item.CaseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<EvaluationTemplate>().WithMany().HasForeignKey(item => item.TemplateId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<RiskRuleVersion>().WithMany().HasForeignKey(item => item.RiskRuleVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.StartedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.SubmittedBy).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<EvaluationResponse>(entity =>
        {
            entity.ToTable("Evaluacion_Respuesta", table => table.HasCheckConstraint(
                "CK_Evaluacion_Respuesta_Opcion", "opcion IN ('C','CP','IT','NA')"));
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.EvaluationInstanceId, item.TemplateItemId }).IsUnique();
            entity.Property(item => item.EvaluationInstanceId).HasColumnName("instancia_id");
            entity.Property(item => item.TemplateItemId).HasColumnName("item_id");
            entity.Property(item => item.OptionCode).HasColumnName("opcion").HasMaxLength(5);
            entity.Property(item => item.Observations).HasColumnName("observaciones").HasColumnType("text");
            entity.Property(item => item.Comments).HasColumnName("comentarios").HasColumnType("text");
            entity.Property(item => item.SavedAt).HasColumnName("fecha_guardado");
            entity.Property(item => item.SavedBy).HasColumnName("guardado_por");
            entity.HasOne<EvaluationInstance>().WithMany().HasForeignKey(item => item.EvaluationInstanceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<EvaluationTemplateItem>().WithMany().HasForeignKey(item => item.TemplateItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.SavedBy).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<EvaluationResult>(entity =>
        {
            entity.ToTable("Evaluacion_Resultado", table =>
            {
                table.HasCheckConstraint("CK_Evaluacion_Resultado_Porcentaje", "porcentaje_bpm BETWEEN 0 AND 100");
                table.HasCheckConstraint("CK_Evaluacion_Resultado_Denominador", "denominador_bpm > 0 AND puntos_bpm >= 0 AND puntos_bpm <= denominador_bpm");
                table.HasCheckConstraint("CK_Evaluacion_Resultado_Frecuencia", "frecuencia_meses > 0");
                table.HasCheckConstraint("CK_Evaluacion_Resultado_Conteos", "cantidad_criticas >= 0 AND cantidad_mayores >= 0 AND cantidad_menores >= 0");
            });
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.EvaluationInstanceId).IsUnique();
            entity.HasIndex(item => item.RiskCalculationId).IsUnique();
            entity.Property(item => item.EvaluationInstanceId).HasColumnName("instancia_id");
            entity.Property(item => item.BpmPoints).HasColumnName("puntos_bpm").HasPrecision(10, 3);
            entity.Property(item => item.BpmDenominator).HasColumnName("denominador_bpm").HasPrecision(10, 3);
            entity.Property(item => item.BpmPercentage).HasColumnName("porcentaje_bpm").HasPrecision(6, 2);
            entity.Property(item => item.QualificationCode).HasColumnName("codigo_calificacion").HasMaxLength(30);
            entity.Property(item => item.Classification).HasColumnName("clasificacion").HasMaxLength(30);
            entity.Property(item => item.BpmRiskScore).HasColumnName("puntaje_riesgo_bpm").HasPrecision(5, 2);
            entity.Property(item => item.CriticalCount).HasColumnName("cantidad_criticas");
            entity.Property(item => item.MajorCount).HasColumnName("cantidad_mayores");
            entity.Property(item => item.MinorCount).HasColumnName("cantidad_menores");
            entity.Property(item => item.RiskCalculationId).HasColumnName("calculo_riesgo_id");
            entity.Property(item => item.FrequencyMonths).HasColumnName("frecuencia_meses");
            entity.Property(item => item.CalculatedAt).HasColumnName("fecha_calculo");
            entity.HasOne<EvaluationInstance>().WithOne().HasForeignKey<EvaluationResult>(item => item.EvaluationInstanceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<RiskCalculation>().WithOne().HasForeignKey<EvaluationResult>(item => item.RiskCalculationId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<EvaluationNonConformity>(entity =>
        {
            entity.ToTable("Evaluacion_No_Conformidad", table => table.HasCheckConstraint(
                "CK_Evaluacion_No_Conformidad_Severidad", "severidad IN ('CRITICAL','MAJOR','MINOR')"));
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.EvaluationResultId, item.GuidanceCriterionId }).IsUnique();
            entity.Property(item => item.EvaluationResultId).HasColumnName("resultado_id");
            entity.Property(item => item.EvaluationResponseId).HasColumnName("respuesta_id");
            entity.Property(item => item.GuidanceCriterionId).HasColumnName("criterio_id");
            entity.Property(item => item.Severity).HasColumnName("severidad").HasMaxLength(20);
            entity.Property(item => item.DetectedAt).HasColumnName("fecha_deteccion");
            entity.HasOne<EvaluationResult>().WithMany().HasForeignKey(item => item.EvaluationResultId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<EvaluationResponse>().WithMany().HasForeignKey(item => item.EvaluationResponseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<EvaluationGuidanceCriterion>().WithMany().HasForeignKey(item => item.GuidanceCriterionId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<EvaluationEvidence>(entity =>
        {
            entity.ToTable("Evaluacion_Evidencia", table =>
            {
                table.HasCheckConstraint("CK_Evaluacion_Evidencia_Tamano", $"tamano_bytes > 0 AND tamano_bytes <= {EvidencePolicy.MaxSizeBytes}");
                table.HasCheckConstraint("CK_Evaluacion_Evidencia_Tipo",
                    $"tipo_mime IN ({string.Join(", ", EvidencePolicy.AllowedMimeTypes.Select(value => $"'{value}'"))})");
                table.HasCheckConstraint("CK_Evaluacion_Evidencia_Hash", "char_length(hash) = 64");
            });
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.EvaluationInstanceId);
            entity.HasIndex(item => item.EvaluationResponseId);
            entity.HasIndex(item => item.StorageKey).IsUnique();
            entity.Property(item => item.EvaluationInstanceId).HasColumnName("instancia_id");
            entity.Property(item => item.EvaluationResponseId).HasColumnName("respuesta_id");
            entity.Property(item => item.FileName).HasColumnName("nombre_archivo").HasMaxLength(260);
            entity.Property(item => item.MimeType).HasColumnName("tipo_mime").HasMaxLength(120);
            entity.Property(item => item.SizeBytes).HasColumnName("tamano_bytes");
            entity.Property(item => item.Hash).HasColumnName("hash").HasMaxLength(128);
            entity.Property(item => item.StorageKey).HasColumnName("clave_objeto").HasMaxLength(500);
            entity.Property(item => item.Description).HasColumnName("descripcion").HasMaxLength(500);
            entity.Property(item => item.UploadedAt).HasColumnName("fecha_carga");
            entity.Property(item => item.UploadedBy).HasColumnName("cargado_por");
            entity.HasOne<EvaluationInstance>().WithMany().HasForeignKey(item => item.EvaluationInstanceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<EvaluationResponse>().WithMany().HasForeignKey(item => item.EvaluationResponseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UploadedBy).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EvaluationReport>(entity =>
        {
            entity.ToTable("Evaluacion_Informe");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.EvaluationInstanceId, item.Version }).IsUnique();
            entity.Property(item => item.EvaluationInstanceId).HasColumnName("instancia_id");
            entity.Property(item => item.Version).HasColumnName("version");
            entity.Property(item => item.Status).HasColumnName("estado").HasMaxLength(20);
            entity.Property(item => item.ExecutiveSummary).HasColumnName("resumen_ejecutivo").HasColumnType("text");
            entity.Property(item => item.Findings).HasColumnName("hallazgos").HasColumnType("text");
            entity.Property(item => item.Recommendations).HasColumnName("recomendaciones").HasColumnType("text");
            entity.Property(item => item.CreatedAt).HasColumnName("fecha_emision");
            entity.Property(item => item.CreatedBy).HasColumnName("emitido_por");
            entity.HasOne<EvaluationInstance>().WithMany().HasForeignKey(item => item.EvaluationInstanceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EvaluationReportReview>(entity =>
        {
            entity.ToTable("Evaluacion_Informe_Revision");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.ReportId);
            entity.Property(item => item.ReportId).HasColumnName("informe_id");
            entity.Property(item => item.Decision).HasColumnName("decision").HasMaxLength(30);
            entity.Property(item => item.Observations).HasColumnName("observaciones").HasColumnType("text");
            entity.Property(item => item.ReviewedAt).HasColumnName("fecha_revision");
            entity.Property(item => item.ReviewedBy).HasColumnName("revisado_por");
            entity.HasOne<EvaluationReport>().WithMany().HasForeignKey(item => item.ReportId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ReviewedBy).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EvaluationOfficialReport>(entity =>
        {
            entity.ToTable("Evaluacion_Informe_Oficial", table =>
            {
                table.HasCheckConstraint("CK_Evaluacion_Informe_Oficial_Tamano", "tamano_bytes > 0");
                table.HasCheckConstraint("CK_Evaluacion_Informe_Oficial_Hash", "char_length(hash_sha256) = 64");
                table.HasCheckConstraint("CK_Evaluacion_Informe_Oficial_Firma", "char_length(firma_base64) > 0");
                table.HasCheckConstraint("CK_Evaluacion_Informe_Oficial_Huella", "char_length(huella_clave_publica) = 64");
            });
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.ReportId).IsUnique();
            entity.HasIndex(item => item.StorageKey).IsUnique();
            entity.Property(item => item.ReportId).HasColumnName("informe_id");
            entity.Property(item => item.FileName).HasColumnName("nombre_archivo").HasMaxLength(260);
            entity.Property(item => item.MimeType).HasColumnName("tipo_mime").HasMaxLength(120);
            entity.Property(item => item.SizeBytes).HasColumnName("tamano_bytes");
            entity.Property(item => item.Sha256).HasColumnName("hash_sha256").HasMaxLength(64);
            entity.Property(item => item.StorageKey).HasColumnName("clave_objeto").HasMaxLength(500);
            entity.Property(item => item.SignatureAlgorithm).HasColumnName("algoritmo_firma").HasMaxLength(30);
            entity.Property(item => item.SignatureBase64).HasColumnName("firma_base64").HasMaxLength(500);
            entity.Property(item => item.PublicKeyThumbprint).HasColumnName("huella_clave_publica").HasMaxLength(64);
            entity.Property(item => item.GeneratedAt).HasColumnName("fecha_generacion");
            entity.Property(item => item.GeneratedBy).HasColumnName("generado_por");
            entity.HasOne<EvaluationReport>().WithMany().HasForeignKey(item => item.ReportId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.GeneratedBy).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CaseClosure>(entity =>
        {
            entity.ToTable("Caso_Cierre", table => table.HasCheckConstraint("CK_Caso_Cierre_Estado", "estado = 'CLOSED'"));
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.CaseId).IsUnique();
            entity.Property(item => item.CaseId).HasColumnName("caso_id");
            entity.Property(item => item.ReportId).HasColumnName("informe_id");
            entity.Property(item => item.OfficialReportId).HasColumnName("informe_oficial_id");
            entity.Property(item => item.Status).HasColumnName("estado").HasMaxLength(20);
            entity.Property(item => item.Result).HasColumnName("resultado").HasColumnType("text");
            entity.Property(item => item.ClosedAt).HasColumnName("fecha_cierre");
            entity.Property(item => item.ClosedBy).HasColumnName("cerrado_por");
            entity.HasOne<InspectionCase>().WithMany().HasForeignKey(item => item.CaseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<EvaluationReport>().WithMany().HasForeignKey(item => item.ReportId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<EvaluationOfficialReport>().WithMany().HasForeignKey(item => item.OfficialReportId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ClosedBy).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Notification>(entity =>
        {
            entity.ToTable("Notificacion");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.RecipientId, item.CreatedAt });
            entity.HasIndex(item => item.OperationId).IsUnique();
            entity.Property(item => item.RecipientId).HasColumnName("destinatario_id");
            entity.Property(item => item.Type).HasColumnName("tipo").HasMaxLength(60);
            entity.Property(item => item.Title).HasColumnName("titulo").HasMaxLength(160);
            entity.Property(item => item.Message).HasColumnName("mensaje").HasColumnType("text");
            entity.Property(item => item.ReferenceType).HasColumnName("tipo_referencia").HasMaxLength(40);
            entity.Property(item => item.ReferenceId).HasColumnName("referencia_id");
            entity.Property(item => item.OperationId).HasColumnName("operacion_id").HasMaxLength(180);
            entity.Property(item => item.CreatedAt).HasColumnName("fecha_creacion");
            entity.Property(item => item.ReadAt).HasColumnName("fecha_lectura");
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.RecipientId).OnDelete(DeleteBehavior.Restrict);
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
        builder.Entity<ActiveEvaluationTemplate>(entity =>
        {
            entity.ToTable("Plantilla_Activa", table => table.HasCheckConstraint(
                "CK_Plantilla_Activa_Singleton", "\"Id\" = 1"));
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).ValueGeneratedNever();
            entity.Property(item => item.TemplateId).HasColumnName("plantilla_id");
            entity.Property(item => item.ActivatedAt).HasColumnName("fecha_activacion");
            entity.Property(item => item.ActivatedBy).HasColumnName("activado_por");
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
