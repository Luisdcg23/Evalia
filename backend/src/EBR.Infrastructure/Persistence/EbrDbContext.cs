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
    public DbSet<EvaluationTemplate> EvaluationTemplates => Set<EvaluationTemplate>();
    public DbSet<EvaluationTemplateItem> EvaluationTemplateItems => Set<EvaluationTemplateItem>();
    public DbSet<BpmRequest> BpmRequests => Set<BpmRequest>();
    public DbSet<InspectionCase> InspectionCases => Set<InspectionCase>();
    public DbSet<CaseStateHistory> CaseStateHistories => Set<CaseStateHistory>();
    public DbSet<HealthAlert> HealthAlerts => Set<HealthAlert>();
    public DbSet<Complaint> Complaints => Set<Complaint>();

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
            entity.Property(item => item.Description).HasColumnName("descripcion").HasMaxLength(2000);
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
    }

    private static void ConfigureRiskCatalogs(ModelBuilder builder)
    {
        builder.Entity<FoodCategory>(entity =>
        {
            entity.ToTable("Categoria_Alimento");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.Name).IsUnique();
            entity.Property(item => item.Name).HasColumnName("nombre").HasMaxLength(120);
        });
        builder.Entity<FoodSubcategory>(entity =>
        {
            entity.ToTable("Subcategoria_Alimento");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.CategoryId, item.Name }).IsUnique();
            entity.Property(item => item.CategoryId).HasColumnName("categoria_id");
            entity.Property(item => item.Name).HasColumnName("nombre").HasMaxLength(160);
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
            entity.HasIndex(item => item.Code).IsUnique();
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
            entity.HasIndex(item => new { item.CompanyId, item.CalculatedAt });
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
