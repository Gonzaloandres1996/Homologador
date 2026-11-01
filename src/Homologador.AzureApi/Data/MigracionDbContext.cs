using Homologador.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Homologador.AzureApi.Data;

public class MigracionDbContext : DbContext
{
    public MigracionDbContext(DbContextOptions<MigracionDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserReport> UserReports => Set<UserReport>();
    public DbSet<DataSource> DataSources => Set<DataSource>();
    public DbSet<QueryDefinition> QueryDefinitions => Set<QueryDefinition>();
    public DbSet<QueryParameter> QueryParameters => Set<QueryParameter>();
    public DbSet<LookupTable> LookupTables => Set<LookupTable>();
    public DbSet<LookupEntry> LookupEntries => Set<LookupEntry>();
    public DbSet<MappingPipeline> MappingPipelines => Set<MappingPipeline>();
    public DbSet<MappingRule> MappingRules => Set<MappingRule>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<RegistroRecepcion> RegistrosRecepcion => Set<RegistroRecepcion>();
    public DbSet<RegistroCalidad> RegistrosCalidad => Set<RegistroCalidad>();
    public DbSet<RegistroProduccion> RegistrosProduccion => Set<RegistroProduccion>();
    public DbSet<ExecutionLog> ExecutionLogs => Set<ExecutionLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("Users", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Codigo).ValueGeneratedOnAddOrUpdate();
            e.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<UserReport>(e =>
        {
            e.ToTable("UserReports", "dbo");
            e.HasKey(x => new { x.UserId, x.ReportId });
            e.HasOne(x => x.User).WithMany(u => u.UserReports).HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Report).WithMany(r => r.UserReports).HasForeignKey(x => x.ReportId);
        });

        modelBuilder.Entity<DataSource>(e =>
        {
            e.ToTable("DataSources", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Codigo).ValueGeneratedOnAddOrUpdate();
            e.Property(x => x.Database).HasColumnName("Database");
        });

        modelBuilder.Entity<QueryDefinition>(e =>
        {
            e.ToTable("QueryDefinitions", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Codigo).ValueGeneratedOnAddOrUpdate();
            e.HasOne(x => x.DataSource).WithMany(d => d.QueryDefinitions).HasForeignKey(x => x.DataSourceId);
        });

        modelBuilder.Entity<QueryParameter>(e =>
        {
            e.ToTable("QueryParameters", "dbo");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.QueryDefinition).WithMany(q => q.Parameters).HasForeignKey(x => x.QueryDefinitionId);
        });

        modelBuilder.Entity<LookupTable>(e =>
        {
            e.ToTable("LookupTables", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Codigo).ValueGeneratedOnAddOrUpdate();
        });

        modelBuilder.Entity<LookupEntry>(e =>
        {
            e.ToTable("LookupEntries", "dbo");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.LookupTable).WithMany(t => t.Entries).HasForeignKey(x => x.LookupTableId);
        });

        modelBuilder.Entity<MappingPipeline>(e =>
        {
            e.ToTable("MappingPipelines", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Codigo).ValueGeneratedOnAddOrUpdate();
        });

        modelBuilder.Entity<MappingRule>(e =>
        {
            e.ToTable("MappingRules", "dbo");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Pipeline).WithMany(p => p.Rules).HasForeignKey(x => x.PipelineId);
            e.HasOne(x => x.LookupTable).WithMany().HasForeignKey(x => x.LookupTableId);
        });

        modelBuilder.Entity<Report>(e =>
        {
            e.ToTable("Reports", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Codigo).ValueGeneratedOnAddOrUpdate();
            e.HasOne(x => x.QueryDefinition).WithMany().HasForeignKey(x => x.QueryDefinitionId);
            e.HasOne(x => x.MappingPipeline).WithMany().HasForeignKey(x => x.MappingPipelineId);
        });

        modelBuilder.Entity<RegistroRecepcion>(e =>
        {
            e.ToTable("RegistrosRecepcion", "dbo");
            e.HasKey(x => x.Lote);
            e.Property(x => x.Kg).HasPrecision(18, 2);
            e.HasOne(x => x.Report).WithMany().HasForeignKey(x => x.ReportId);
        });

        modelBuilder.Entity<RegistroCalidad>(e =>
        {
            e.ToTable("RegistrosCalidad", "dbo");
            e.HasKey(x => x.Lote);
            e.HasOne(x => x.Report).WithMany().HasForeignKey(x => x.ReportId);
        });

        modelBuilder.Entity<RegistroProduccion>(e =>
        {
            e.ToTable("RegistrosProduccion", "dbo");
            e.HasKey(x => new { x.ReportId, x.LoteProceso, x.CodigoPallet });
            e.HasOne(x => x.Report).WithMany().HasForeignKey(x => x.ReportId);
        });

        modelBuilder.Entity<ExecutionLog>(e =>
        {
            e.ToTable("ExecutionLogs", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.DurationSeconds).HasPrecision(10, 3);
            e.HasOne(x => x.Report).WithMany().HasForeignKey(x => x.ReportId);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        });
    }
}
