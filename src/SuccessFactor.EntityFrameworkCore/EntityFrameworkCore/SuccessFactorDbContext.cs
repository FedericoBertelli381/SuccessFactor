using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuccessFactor.Auditing;
using SuccessFactor.Competencies;
using SuccessFactor.Competencies.Assessments;
using SuccessFactor.Competencies.Models;
using SuccessFactor.Cycles;
using SuccessFactor.Employees;
using SuccessFactor.Goals;
using SuccessFactor.Goals.Importing;
using SuccessFactor.JobRoles;
using SuccessFactor.OrgUnits;
using SuccessFactor.Process;
using SuccessFactor.Workflow;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using Volo.Abp.BlobStoring.Database.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.OpenIddict.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.SettingManagement.EntityFrameworkCore;
using Volo.Abp.TenantManagement;
using Volo.Abp.TenantManagement.EntityFrameworkCore;

namespace SuccessFactor.EntityFrameworkCore;

[ReplaceDbContext(typeof(IIdentityDbContext))]
[ReplaceDbContext(typeof(ITenantManagementDbContext))]
[ConnectionStringName("Default")]
public class SuccessFactorDbContext :
    AbpDbContext<SuccessFactorDbContext>,
    ITenantManagementDbContext,
    IIdentityDbContext
{
    /* Add DbSet properties for your Aggregate Roots / Entities here. */
    public DbSet<SuccessFactor.Goals.Goal> Goals { get; set; } = default!;
    public DbSet<Competency> Competencies { get; set; } = default!;
    public DbSet<Employee> Employees { get; set; } = default!;
    public DbSet<EmployeeManager> EmployeeManagers { get; set; } = default!;
    public DbSet<OrgUnit> OrgUnits { get; set; } = default!;
    public DbSet<JobRole> JobRoles { get; set; } = default!;
    public DbSet<ProcessTemplate> ProcessTemplates { get; set; } = default!;
    public DbSet<Cycle> Cycles { get; set; } = default!;
    public DbSet<SuccessFactor.Goals.GoalAssignment> GoalAssignments { get; set; } = default!;
    public DbSet<SuccessFactor.Goals.GoalProgressEntry> GoalProgressEntries { get; set; } = default!;
    public DbSet<SuccessFactor.Goals.Importing.GoalImportBatch> GoalImportBatches { get; set; } = default!;
    public DbSet<SuccessFactor.Goals.Importing.GoalImportRow> GoalImportRows { get; set; } = default!;
    public DbSet<SuccessFactor.Goals.Importing.GoalProgressBatch> GoalProgressBatches { get; set; } = default!;
    public DbSet<SuccessFactor.Goals.Importing.GoalProgressRow> GoalProgressRows { get; set; } = default!;
    public DbSet<SuccessFactor.Competencies.Models.CompetencyModel> CompetencyModels { get; set; } = default!;
    public DbSet<SuccessFactor.Competencies.Models.CompetencyModelItem> CompetencyModelItems { get; set; } = default!;
    public DbSet<SuccessFactor.Competencies.Assessments.CompetencyAssessment> CompetencyAssessments { get; set; } = default!;
    public DbSet<SuccessFactor.Competencies.Assessments.CompetencyAssessmentItem> CompetencyAssessmentItems { get; set; } = default!;
    public DbSet<SuccessFactor.Workflow.ProcessPhase> ProcessPhases { get; set; } = default!;
    public DbSet<SuccessFactor.Workflow.PhaseTransition> PhaseTransitions { get; set; } = default!;
    public DbSet<SuccessFactor.Cycles.CycleParticipant> CycleParticipants { get; set; } = default!;
    public DbSet<SuccessFactor.Workflow.PhaseRolePermission> PhaseRolePermissions { get; set; } = default!;
    public DbSet<SuccessFactor.Workflow.PhaseFieldPolicy> PhaseFieldPolicies { get; set; } = default!;
    public DbSet<BusinessAuditEvent> BusinessAuditEvents { get; set; } = default!;

    #region Entities from the modules

    /* Notice: We only implemented IIdentityProDbContext and ISaasDbContext
     * and replaced them for this DbContext. This allows you to perform JOIN
     * queries for the entities of these modules over the repositories easily. You
     * typically don't need that for other modules. But, if you need, you can
     * implement the DbContext interface of the needed module and use ReplaceDbContext
     * attribute just like IIdentityProDbContext and ISaasDbContext.
     *
     * More info: Replacing a DbContext of a module ensures that the related module
     * uses this DbContext on runtime. Otherwise, it will use its own DbContext class.
     */

    // Identity
    public DbSet<IdentityUser> Users { get; set; }
    public DbSet<IdentityRole> Roles { get; set; }
    public DbSet<IdentityClaimType> ClaimTypes { get; set; }
    public DbSet<OrganizationUnit> OrganizationUnits { get; set; }
    public DbSet<IdentitySecurityLog> SecurityLogs { get; set; }
    public DbSet<IdentityLinkUser> LinkUsers { get; set; }
    public DbSet<IdentityUserDelegation> UserDelegations { get; set; }
    public DbSet<IdentitySession> Sessions { get; set; }

    // Tenant Management
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<TenantConnectionString> TenantConnectionStrings { get; set; }

    #endregion

    public SuccessFactorDbContext(DbContextOptions<SuccessFactorDbContext> options)
        : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        /* Include modules to your migration db context */

        builder.ConfigurePermissionManagement();
        builder.ConfigureSettingManagement();
        builder.ConfigureBackgroundJobs();
        builder.ConfigureAuditLogging();
        builder.ConfigureFeatureManagement();
        builder.ConfigureIdentity();
        builder.ConfigureOpenIddict();
        builder.ConfigureTenantManagement();
        builder.ConfigureBlobStoring();

        /* Configure your own tables/entities inside here */

        //builder.Entity<YourEntity>(b =>
        //{
        //    b.ToTable(SuccessFactorConsts.DbTablePrefix + "YourEntities", SuccessFactorConsts.DbSchema);
        //    b.ConfigureByConvention(); //auto configure for the base class props
        //    //...
        //});
        builder.Entity<Goal>(b =>
        {
            b.ToTable("Goals", "dbo");
            b.ConfigureByConvention(); // best practice ABP :contentReference[oaicite:4]{index=4}

            // PK: proprietà ABP "Id" -> colonna "GoalId"
            b.Property(x => x.Id).HasColumnName("GoalId");

            // TenantId (nel DB è NOT NULL: lo forziamo)
            b.Property(x => x.TenantId).HasColumnName("TenantId").IsRequired();

            // Campi
            b.Property(x => x.Title).HasMaxLength(300).IsRequired();
            b.Property(x => x.Category).HasMaxLength(100);
            b.Property(x => x.DefaultWeight).HasColumnType("decimal(5,2)");

            // Auditing: rimappa i nomi colonna
            b.Property(x => x.CreationTime).HasColumnName("CreatedAt");
            b.Property(x => x.CreatorId).HasColumnName("CreatedByUserId");
            b.Property(x => x.LastModificationTime).HasColumnName("ModifiedAt");
            b.Property(x => x.LastModifierId).HasColumnName("ModifiedByUserId");

            // Concurrency: rowversion
            b.Property(x => x.RowVer)
                .HasColumnName("RowVer")
                .IsRowVersion()
                .IsConcurrencyToken();

            // Indici (coerenti con lo script DB v1)
            b.HasIndex(x => new { x.TenantId, x.IsLibraryItem });
        });
        builder.Entity<Competency>(b =>
        {
            b.ToTable("Competencies", "dbo");
            b.ConfigureByConvention();

            b.Property(x => x.Id).HasColumnName("CompetencyId");

            b.Property(x => x.TenantId).HasColumnName("TenantId").IsRequired();

            b.Property(x => x.Code).HasColumnName("Code").HasMaxLength(50).IsRequired();
            b.Property(x => x.Name).HasColumnName("Name").HasMaxLength(200).IsRequired();

            b.Property(x => x.IsActive).HasColumnName("IsActive").IsRequired();

            // Description è NVARCHAR(MAX), non serve HasMaxLength
            b.Property(x => x.Description).HasColumnName("Description");

            b.Property(x => x.RowVer)
                .HasColumnName("RowVer")
                .IsRowVersion()
                .IsConcurrencyToken();

            b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.IsActive });
        });
        builder.Entity<Employee>(b =>
        {
            b.ToTable("Employees", "dbo");
            b.ConfigureByConvention();

            b.Property(x => x.Id).HasColumnName("EmployeeId");

            b.Property(x => x.TenantId).HasColumnName("TenantId").IsRequired();

            b.Property(x => x.UserId).HasColumnName("UserId");
            b.Property(x => x.Matricola).HasColumnName("Matricola").HasMaxLength(50).IsRequired();
            b.Property(x => x.FullName).HasColumnName("FullName").HasMaxLength(200).IsRequired();
            b.Property(x => x.Email).HasColumnName("Email").HasMaxLength(256);

            b.Property(x => x.OrgUnitId).HasColumnName("OrgUnitId");
            b.Property(x => x.JobRoleId).HasColumnName("JobRoleId");

            b.Property(x => x.IsActive).HasColumnName("IsActive").IsRequired();

            // auditing mapping
            b.Property(x => x.CreationTime).HasColumnName("CreatedAt");
            b.Property(x => x.CreatorId).HasColumnName("CreatedByUserId");
            b.Property(x => x.LastModificationTime).HasColumnName("ModifiedAt");
            b.Property(x => x.LastModifierId).HasColumnName("ModifiedByUserId");

            // rowversion
            b.Property(x => x.RowVer)
                .HasColumnName("RowVer")
                .IsRowVersion()
                .IsConcurrencyToken();

            // Indice univoco: TenantId + Matricola
            b.HasIndex(x => new { x.TenantId, x.Matricola }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.UserId });
            b.HasIndex(x => new { x.TenantId, x.OrgUnitId });
            b.HasIndex(x => new { x.TenantId, x.JobRoleId });
            b.HasIndex(x => new { x.TenantId, x.IsActive });
        });
        builder.Entity<EmployeeManager>(b =>
        {
            b.ToTable("EmployeeManagers", "dbo");
            b.ConfigureByConvention();

            b.Property(x => x.Id).HasColumnName("EmployeeManagerId").ValueGeneratedOnAdd(); ;
            b.Property(x => x.TenantId).HasColumnName("TenantId").IsRequired();

            b.Property(x => x.EmployeeId).HasColumnName("EmployeeId").IsRequired();
            b.Property(x => x.ManagerEmployeeId).HasColumnName("ManagerEmployeeId").IsRequired();

            b.Property(x => x.RelationType).HasColumnName("RelationType").HasMaxLength(30).IsRequired();
            b.Property(x => x.IsPrimary).HasColumnName("IsPrimary").IsRequired();

            b.Property(x => x.StartDate).HasColumnName("StartDate");
            b.Property(x => x.EndDate).HasColumnName("EndDate");

            b.Property(x => x.CreationTime).HasColumnName("CreatedAt");
            b.Property(x => x.CreatorId).HasColumnName("CreatedByUserId");
            b.Property(x => x.LastModificationTime).HasColumnName("ModifiedAt");
            b.Property(x => x.LastModifierId).HasColumnName("ModifiedByUserId");

            b.Property(x => x.RowVer)
                .HasColumnName("RowVer")
                .IsRowVersion()
                .IsConcurrencyToken();

            b.HasIndex(x => new { x.TenantId, x.EmployeeId, x.ManagerEmployeeId, x.RelationType }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.ManagerEmployeeId });
            b.HasIndex(x => new { x.TenantId, x.EmployeeId });
        });
        builder.Entity<OrgUnit>(b =>
        {
            b.ToTable("OrgUnits", "dbo");
            b.ConfigureByConvention();

            b.Property(x => x.Id).HasColumnName("OrgUnitId");

            b.Property(x => x.TenantId).HasColumnName("TenantId").IsRequired();

            b.Property(x => x.Name).HasColumnName("Name").HasMaxLength(200).IsRequired();
            b.Property(x => x.ParentOrgUnitId).HasColumnName("ParentOrgUnitId");

            // auditing mapping
            b.Property(x => x.CreationTime).HasColumnName("CreatedAt");
            b.Property(x => x.CreatorId).HasColumnName("CreatedByUserId");
            b.Property(x => x.LastModificationTime).HasColumnName("ModifiedAt");
            b.Property(x => x.LastModifierId).HasColumnName("ModifiedByUserId");

            // rowversion
            b.Property(x => x.RowVer)
                .HasColumnName("RowVer")
                .IsRowVersion()
                .IsConcurrencyToken();

            // indici coerenti col DB v1
            b.HasIndex(x => new { x.TenantId, x.ParentOrgUnitId });
            b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        });
        builder.Entity<JobRole>(b =>
        {
            b.ToTable("JobRoles", "dbo");
            b.ConfigureByConvention();

            b.Property(x => x.Id).HasColumnName("JobRoleId");

            b.Property(x => x.TenantId).HasColumnName("TenantId").IsRequired();

            b.Property(x => x.Name).HasColumnName("Name").HasMaxLength(200).IsRequired();

            b.Property(x => x.CreationTime).HasColumnName("CreatedAt");
            b.Property(x => x.CreatorId).HasColumnName("CreatedByUserId");
            b.Property(x => x.LastModificationTime).HasColumnName("ModifiedAt");
            b.Property(x => x.LastModifierId).HasColumnName("ModifiedByUserId");

            b.Property(x => x.RowVer)
                .HasColumnName("RowVer")
                .IsRowVersion()
                .IsConcurrencyToken();

            b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        });
        builder.Entity<ProcessTemplate>(b =>
        {
            b.ToTable("ProcessTemplates", "dbo");
            b.ConfigureByConvention();

            b.Property(x => x.Id).HasColumnName("TemplateId");
            b.Property(x => x.TenantId).HasColumnName("TenantId").IsRequired();

            b.Property(x => x.Name).HasColumnName("Name").HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasColumnName("Version").IsRequired();
            b.Property(x => x.IsDefault).HasColumnName("IsDefault").IsRequired();

            b.Property(x => x.CreationTime).HasColumnName("CreatedAt");
            b.Property(x => x.CreatorId).HasColumnName("CreatedByUserId");
            b.Property(x => x.LastModificationTime).HasColumnName("ModifiedAt");
            b.Property(x => x.LastModifierId).HasColumnName("ModifiedByUserId");

            b.Property(x => x.RowVer).HasColumnName("RowVer").IsRowVersion().IsConcurrencyToken();

            b.HasIndex(x => new { x.TenantId, x.Name, x.Version }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.IsDefault });
        });
        builder.Entity<Cycle>(b =>
        {
            b.ToTable("Cycles", "dbo");
            b.ConfigureByConvention();

            b.Property(x => x.Id).HasColumnName("CycleId");
            b.Property(x => x.TenantId).HasColumnName("TenantId").IsRequired();

            b.Property(x => x.Name).HasColumnName("Name").HasMaxLength(200).IsRequired();
            b.Property(x => x.CycleYear).HasColumnName("CycleYear").IsRequired();

            b.Property(x => x.TemplateId).HasColumnName("TemplateId").IsRequired();
            b.Property(x => x.CurrentPhaseId).HasColumnName("CurrentPhaseId");

            b.Property(x => x.Status).HasColumnName("Status").HasMaxLength(30).IsRequired();
            b.Property(x => x.StartDate).HasColumnName("StartDate").HasColumnType("date");
            b.Property(x => x.EndDate).HasColumnName("EndDate").HasColumnType("date");

            b.Property(x => x.CreationTime).HasColumnName("CreatedAt");
            b.Property(x => x.CreatorId).HasColumnName("CreatedByUserId");
            b.Property(x => x.LastModificationTime).HasColumnName("ModifiedAt");
            b.Property(x => x.LastModifierId).HasColumnName("ModifiedByUserId");

            b.Property(x => x.RowVer).HasColumnName("RowVer").IsRowVersion().IsConcurrencyToken();

            b.HasIndex(x => new { x.TenantId, x.CycleYear });
            b.HasIndex(x => new { x.TenantId, x.Status, x.CycleYear });
            b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        });
        builder.Entity<GoalAssignment>(b =>
        {
            b.ToTable("GoalAssignments", "dbo");
            b.ConfigureByConvention();

            b.Property(x => x.Id).HasColumnName("AssignmentId");

            b.Property(x => x.TenantId).HasColumnName("TenantId").IsRequired();

            b.Property(x => x.CycleId).HasColumnName("CycleId").IsRequired();
            b.Property(x => x.EmployeeId).HasColumnName("EmployeeId").IsRequired();
            b.Property(x => x.GoalId).HasColumnName("GoalId").IsRequired();

            b.Property(x => x.Weight).HasColumnName("Weight").HasColumnType("decimal(5,2)").IsRequired();
            b.Property(x => x.TargetValue).HasColumnName("TargetValue").HasColumnType("decimal(18,4)");

            b.Property(x => x.StartDate).HasColumnName("StartDate").HasColumnType("date");
            b.Property(x => x.DueDate).HasColumnName("DueDate").HasColumnType("date");

            b.Property(x => x.Status).HasColumnName("Status").HasMaxLength(30).IsRequired();

            b.Property(x => x.CreationTime).HasColumnName("CreatedAt");
            b.Property(x => x.CreatorId).HasColumnName("CreatedByUserId");
            b.Property(x => x.LastModificationTime).HasColumnName("ModifiedAt");
            b.Property(x => x.LastModifierId).HasColumnName("ModifiedByUserId");

            b.Property(x => x.RowVer).HasColumnName("RowVer").IsRowVersion().IsConcurrencyToken();

            b.HasIndex(x => new { x.TenantId, x.CycleId, x.EmployeeId });
        });
        builder.Entity<GoalProgressEntry>(b =>
        {
            b.ToTable("GoalProgressEntries", "dbo");
            b.ConfigureByConvention();

            b.Property(x => x.Id).HasColumnName("EntryId");
            b.Property(x => x.TenantId).HasColumnName("TenantId").IsRequired();

            b.Property(x => x.AssignmentId).HasColumnName("AssignmentId").IsRequired();

            b.Property(x => x.EntryDate).HasColumnName("EntryDate").HasColumnType("date").IsRequired();
            b.Property(x => x.ProgressPercent).HasColumnName("ProgressPercent").HasColumnType("decimal(5,2)");
            b.Property(x => x.ActualValue).HasColumnName("ActualValue").HasColumnType("decimal(18,4)");
            b.Property(x => x.Note).HasColumnName("Note").HasMaxLength(2000);
            b.Property(x => x.AttachmentId).HasColumnName("AttachmentId");

            b.Property(x => x.CreationTime).HasColumnName("CreatedAt");
            b.Property(x => x.CreatorId).HasColumnName("CreatedByUserId");

            b.Property(x => x.RowVer).HasColumnName("RowVer").IsRowVersion().IsConcurrencyToken();

            b.HasIndex(x => new { x.AssignmentId, x.EntryDate });
        });
        // GoalImportBatches
        builder.Entity<GoalImportBatch>(b =>
        {
            b.ToTable("GoalImportBatches", "dbo");
            b.ConfigureByConvention();

            b.Property(x => x.Id).HasColumnName("BatchId").ValueGeneratedOnAdd();
            b.Property(x => x.TenantId).HasColumnName("TenantId").IsRequired();
            b.Property(x => x.CycleId).HasColumnName("CycleId").IsRequired();
            b.Property(x => x.FileName).HasColumnName("FileName").HasMaxLength(260).IsRequired();
            b.Property(x => x.Status).HasColumnName("Status").HasMaxLength(30).IsRequired();

            b.Property(x => x.CreationTime).HasColumnName("CreatedAt");
            b.Property(x => x.CreatorId).HasColumnName("CreatedByUserId");

            b.HasIndex(x => new { x.TenantId, x.CreationTime });
        });

        // GoalImportRows
        builder.Entity<GoalImportRow>(b =>
        {
            b.ToTable("GoalImportRows", "dbo");
            b.ConfigureByConvention();

            b.Property(x => x.Id).HasColumnName("RowId").ValueGeneratedOnAdd();
            b.Property(x => x.BatchId).HasColumnName("BatchId").IsRequired();
            b.Property(x => x.RowNumber).HasColumnName("RowNumber").IsRequired();
            b.Property(x => x.RawJson).HasColumnName("RawJson").IsRequired();
            b.Property(x => x.ValidationStatus).HasColumnName("ValidationStatus").HasMaxLength(20).IsRequired();
            b.Property(x => x.ErrorMessage).HasColumnName("ErrorMessage").HasMaxLength(2000);

            b.HasIndex(x => new { x.BatchId, x.ValidationStatus });
        });

        // GoalProgressBatches
        builder.Entity<GoalProgressBatch>(b =>
        {
            b.ToTable("GoalProgressBatches", "dbo");
            b.ConfigureByConvention();

            b.Property(x => x.Id).HasColumnName("BatchId").ValueGeneratedOnAdd();
            b.Property(x => x.TenantId).HasColumnName("TenantId").IsRequired();
            b.Property(x => x.CycleId).HasColumnName("CycleId").IsRequired();
            b.Property(x => x.FileName).HasColumnName("FileName").HasMaxLength(260).IsRequired();
            b.Property(x => x.Status).HasColumnName("Status").HasMaxLength(30).IsRequired();

            b.Property(x => x.CreationTime).HasColumnName("CreatedAt");
            b.Property(x => x.CreatorId).HasColumnName("CreatedByUserId");

            b.HasIndex(x => new { x.TenantId, x.CreationTime });
        });

        // GoalProgressRows
        builder.Entity<GoalProgressRow>(b =>
        {
            b.ToTable("GoalProgressRows", "dbo");
            b.ConfigureByConvention();

            b.Property(x => x.Id).HasColumnName("RowId").ValueGeneratedOnAdd();
            b.Property(x => x.BatchId).HasColumnName("BatchId").IsRequired();
            b.Property(x => x.RowNumber).HasColumnName("RowNumber").IsRequired();
            b.Property(x => x.RawJson).HasColumnName("RawJson").IsRequired();
            b.Property(x => x.ValidationStatus).HasColumnName("ValidationStatus").HasMaxLength(20).IsRequired();
            b.Property(x => x.ErrorMessage).HasColumnName("ErrorMessage").HasMaxLength(2000);

            b.HasIndex(x => new { x.BatchId, x.ValidationStatus });
        });
        builder.Entity<CompetencyModel>(b =>
        {
            b.ToTable("CompetencyModels", "dbo");
            b.ConfigureByConvention();

            b.Property(x => x.Id).HasColumnName("ModelId").ValueGeneratedOnAdd();
            b.Property(x => x.TenantId).HasColumnName("TenantId").IsRequired();

            b.Property(x => x.Name).HasColumnName("Name").HasMaxLength(200).IsRequired();
            b.Property(x => x.ScaleType).HasColumnName("ScaleType").HasMaxLength(30).IsRequired();
            b.Property(x => x.MinScore).HasColumnName("MinScore").IsRequired();
            b.Property(x => x.MaxScore).HasColumnName("MaxScore").IsRequired();

            b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        });

        builder.Entity<CompetencyModelItem>(b =>
        {
            b.ToTable("CompetencyModelItems", "dbo");
            b.ConfigureByConvention();

            b.Property(x => x.Id).HasColumnName("ModelItemId").ValueGeneratedOnAdd();
            b.Property(x => x.TenantId).HasColumnName("TenantId").IsRequired();

            b.Property(x => x.ModelId).HasColumnName("ModelId").IsRequired();
            b.Property(x => x.CompetencyId).HasColumnName("CompetencyId").IsRequired();
            b.Property(x => x.Weight).HasColumnName("Weight").HasColumnType("decimal(5,2)");
            b.Property(x => x.IsRequired).HasColumnName("IsRequired").IsRequired();

            b.HasIndex(x => new { x.ModelId, x.CompetencyId }).IsUnique();
        });
        builder.Entity<CompetencyAssessment>(b =>
        {
            b.ToTable("CompetencyAssessments", "dbo");
            b.ConfigureByConvention();

            b.Property(x => x.Id).HasColumnName("AssessmentId").ValueGeneratedOnAdd();
            b.Property(x => x.TenantId).HasColumnName("TenantId").IsRequired();

            b.Property(x => x.CycleId).HasColumnName("CycleId").IsRequired();
            b.Property(x => x.EmployeeId).HasColumnName("EmployeeId").IsRequired();
            b.Property(x => x.EvaluatorEmployeeId).HasColumnName("EvaluatorEmployeeId").IsRequired();

            b.Property(x => x.ModelId).HasColumnName("ModelId"); // se non esiste nel DB, commenta questa riga e la prop nella entity
            b.Property(x => x.AssessmentType).HasColumnName("AssessmentType").HasMaxLength(30).IsRequired();
            b.Property(x => x.Status).HasColumnName("Status").HasMaxLength(30).IsRequired();

            b.Property(x => x.CreationTime).HasColumnName("CreatedAt");
            b.Property(x => x.CreatorId).HasColumnName("CreatedByUserId");
            b.Property(x => x.LastModificationTime).HasColumnName("ModifiedAt");
            b.Property(x => x.LastModifierId).HasColumnName("ModifiedByUserId");

            b.Property(x => x.RowVer).HasColumnName("RowVer").IsRowVersion().IsConcurrencyToken();

            b.HasIndex(x => new { x.CycleId, x.EmployeeId });
            b.HasIndex(x => new { x.TenantId, x.CycleId, x.EmployeeId });
            b.HasIndex(x => new { x.TenantId, x.CycleId, x.EvaluatorEmployeeId });
            b.HasIndex(x => new { x.TenantId, x.CycleId, x.Status });
        });

        builder.Entity<CompetencyAssessmentItem>(b =>
        {
            b.ToTable("CompetencyAssessmentItems", "dbo");
            b.ConfigureByConvention();

            b.Property(x => x.Id).HasColumnName("ItemId").ValueGeneratedOnAdd();
            b.Property(x => x.TenantId).HasColumnName("TenantId").IsRequired();

            b.Property(x => x.AssessmentId).HasColumnName("AssessmentId").IsRequired();
            b.Property(x => x.CompetencyId).HasColumnName("CompetencyId").IsRequired();
            b.Property(x => x.Score).HasColumnName("Score");
            b.Property(x => x.Comment).HasColumnName("Comment").HasMaxLength(2000);
            b.Property(x => x.EvidenceAttachmentId).HasColumnName("EvidenceAttachmentId");

            b.HasIndex(x => new { x.AssessmentId, x.CompetencyId }).IsUnique();
        });
        builder.Entity<ProcessPhase>(b =>
        {
            b.ToTable("ProcessPhases", "dbo");
            b.ConfigureByConvention();

            b.Property(x => x.Id).HasColumnName("PhaseId").ValueGeneratedOnAdd();
            b.Property(x => x.TemplateId).HasColumnName("TemplateId").IsRequired();

            b.Property(x => x.Code).HasColumnName("Code").HasMaxLength(50).IsRequired();
            b.Property(x => x.Name).HasColumnName("Name").HasMaxLength(200).IsRequired();
            b.Property(x => x.PhaseOrder).HasColumnName("PhaseOrder").IsRequired();
            b.Property(x => x.IsTerminal).HasColumnName("IsTerminal").IsRequired();

            b.Property(x => x.StartRule).HasColumnName("StartRule").HasMaxLength(2000);
            b.Property(x => x.EndRule).HasColumnName("EndRule").HasMaxLength(2000);

            b.Property(x => x.CreationTime).HasColumnName("CreatedAt");
            b.Property(x => x.CreatorId).HasColumnName("CreatedByUserId");
            b.Property(x => x.LastModificationTime).HasColumnName("ModifiedAt");
            b.Property(x => x.LastModifierId).HasColumnName("ModifiedByUserId");

            b.Property(x => x.RowVer).HasColumnName("RowVer").IsRowVersion().IsConcurrencyToken();

            b.HasIndex(x => new { x.TemplateId, x.Code }).IsUnique();
            b.HasIndex(x => new { x.TemplateId, x.PhaseOrder });
        });

        builder.Entity<PhaseTransition>(b =>
        {
            b.ToTable("PhaseTransitions", "dbo");
            b.ConfigureByConvention();

            b.Property(x => x.Id).HasColumnName("TransitionId").ValueGeneratedOnAdd();
            b.Property(x => x.TemplateId).HasColumnName("TemplateId").IsRequired();
            b.Property(x => x.FromPhaseId).HasColumnName("FromPhaseId").IsRequired();
            b.Property(x => x.ToPhaseId).HasColumnName("ToPhaseId").IsRequired();
            b.Property(x => x.ConditionExpr).HasColumnName("ConditionExpr").HasMaxLength(2000);

            b.HasIndex(x => new { x.TemplateId, x.FromPhaseId });
        });
        builder.Entity<CycleParticipant>(b =>
        {
            b.ToTable("CycleParticipants", "dbo");
            b.ConfigureByConvention();

            b.Property(x => x.Id).HasColumnName("CycleParticipantId").ValueGeneratedOnAdd();
            b.Property(x => x.TenantId).HasColumnName("TenantId").IsRequired();

            b.Property(x => x.CycleId).HasColumnName("CycleId").IsRequired();
            b.Property(x => x.EmployeeId).HasColumnName("EmployeeId").IsRequired();
            b.Property(x => x.CurrentPhaseId).HasColumnName("CurrentPhaseId");
            b.Property(x => x.Status).HasColumnName("Status").HasMaxLength(30).IsRequired();

            b.Property(x => x.CreationTime).HasColumnName("CreatedAt");
            b.Property(x => x.CreatorId).HasColumnName("CreatedByUserId");

            b.Property(x => x.RowVer).HasColumnName("RowVer").IsRowVersion().IsConcurrencyToken();

            b.HasIndex(x => new { x.CycleId, x.EmployeeId }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.CycleId });
        });
        builder.Entity<PhaseRolePermission>(b =>
        {
            b.ToTable("PhaseRolePermissions", "dbo");
            b.ConfigureByConvention();

            b.Property(x => x.Id).HasColumnName("PermissionId").ValueGeneratedOnAdd();
            b.Property(x => x.TemplateId).HasColumnName("TemplateId").IsRequired();
            b.Property(x => x.PhaseId).HasColumnName("PhaseId").IsRequired();

            b.Property(x => x.RoleCode).HasColumnName("RoleCode").HasMaxLength(50).IsRequired();

            b.Property(x => x.CanView).HasColumnName("CanView");
            b.Property(x => x.CanEdit).HasColumnName("CanEdit");
            b.Property(x => x.CanSubmit).HasColumnName("CanSubmit");
            b.Property(x => x.CanAdvance).HasColumnName("CanAdvance");
            b.Property(x => x.ConditionExpr).HasColumnName("ConditionExpr").HasMaxLength(2000);

            b.Property(x => x.CreationTime).HasColumnName("CreatedAt");
            b.Property(x => x.CreatorId).HasColumnName("CreatedByUserId");
            b.Property(x => x.LastModificationTime).HasColumnName("ModifiedAt");
            b.Property(x => x.LastModifierId).HasColumnName("ModifiedByUserId");

            b.Property(x => x.RowVer).HasColumnName("RowVer").IsRowVersion().IsConcurrencyToken();

            b.HasIndex(x => new { x.TemplateId, x.PhaseId, x.RoleCode }).IsUnique();
        });

        builder.Entity<PhaseFieldPolicy>(b =>
        {
            b.ToTable("PhaseFieldPolicies", "dbo");
            b.ConfigureByConvention();

            b.Property(x => x.Id).HasColumnName("PolicyId").ValueGeneratedOnAdd();
            b.Property(x => x.TemplateId).HasColumnName("TemplateId").IsRequired();
            b.Property(x => x.PhaseId).HasColumnName("PhaseId").IsRequired();

            b.Property(x => x.FieldKey).HasColumnName("FieldKey").HasMaxLength(100).IsRequired();
            b.Property(x => x.RoleCode).HasColumnName("RoleCode").HasMaxLength(50).IsRequired();

            b.Property(x => x.Access).HasColumnName("Access").HasMaxLength(20).IsRequired();
            b.Property(x => x.IsRequired).HasColumnName("IsRequired").IsRequired();
            b.Property(x => x.ConditionExpr).HasColumnName("ConditionExpr").HasMaxLength(2000);

            b.Property(x => x.CreationTime).HasColumnName("CreatedAt");
            b.Property(x => x.CreatorId).HasColumnName("CreatedByUserId");
            b.Property(x => x.LastModificationTime).HasColumnName("ModifiedAt");
            b.Property(x => x.LastModifierId).HasColumnName("ModifiedByUserId");

            b.Property(x => x.RowVer).HasColumnName("RowVer").IsRowVersion().IsConcurrencyToken();

            b.HasIndex(x => new { x.TemplateId, x.PhaseId, x.FieldKey, x.RoleCode }).IsUnique();
        });
        builder.Entity<BusinessAuditEvent>(b =>
        {
            b.ToTable("BusinessAuditEvents", "dbo");
            b.ConfigureByConvention();

            b.Property(x => x.Id).HasColumnName("BusinessAuditEventId").ValueGeneratedOnAdd();
            b.Property(x => x.TenantId).HasColumnName("TenantId");
            b.Property(x => x.UserId).HasColumnName("UserId");
            b.Property(x => x.UserName).HasColumnName("UserName").HasMaxLength(256);
            b.Property(x => x.Action).HasColumnName("Action").HasMaxLength(100).IsRequired();
            b.Property(x => x.EntityType).HasColumnName("EntityType").HasMaxLength(200).IsRequired();
            b.Property(x => x.EntityId).HasColumnName("EntityId").HasMaxLength(100);
            b.Property(x => x.EventTime).HasColumnName("EventTime").IsRequired();
            b.Property(x => x.Payload).HasColumnName("Payload");

            b.HasIndex(x => new { x.TenantId, x.EventTime });
            b.HasIndex(x => new { x.TenantId, x.Action });
            b.HasIndex(x => new { x.TenantId, x.EntityType });
            b.HasIndex(x => new { x.TenantId, x.UserName });
        });
    }

}
