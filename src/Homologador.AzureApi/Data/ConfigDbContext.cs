using Homologador.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Homologador.AzureApi.Data;

public class ConfigDbContext : DbContext
{
    public ConfigDbContext(DbContextOptions<ConfigDbContext> options) : base(options)
    {
    }

    public DbSet<DataSource> DataSources => Set<DataSource>();
    public DbSet<QueryDefinition> QueryDefinitions => Set<QueryDefinition>();
    public DbSet<QueryParameter> QueryParameters => Set<QueryParameter>();
    public DbSet<LookupTable> LookupTables => Set<LookupTable>();
    public DbSet<LookupEntry> LookupEntries => Set<LookupEntry>();
    public DbSet<MappingPipeline> MappingPipelines => Set<MappingPipeline>();
    public DbSet<MappingRule> MappingRules => Set<MappingRule>();
    public DbSet<Report> Reports => Set<Report>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DataSource>(e =>
        {
            e.ToTable("DataSources", "dbo");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Codigo).IsUnique();
        });

        modelBuilder.Entity<QueryDefinition>(e =>
        {
            e.ToTable("QueryDefinitions", "dbo");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Codigo).IsUnique();
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
            e.HasIndex(x => x.Codigo).IsUnique();
        });

        modelBuilder.Entity<LookupEntry>(e =>
        {
            e.ToTable("LookupEntries", "dbo");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.LookupTableId, x.KeyValue }).IsUnique();
            e.HasOne(x => x.LookupTable).WithMany(t => t.Entries).HasForeignKey(x => x.LookupTableId);
        });

        modelBuilder.Entity<MappingPipeline>(e =>
        {
            e.ToTable("MappingPipelines", "dbo");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Codigo).IsUnique();
        });

        modelBuilder.Entity<MappingRule>(e =>
        {
            e.ToTable("MappingRules", "dbo");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.PipelineId, x.SortOrder });
            e.HasOne(x => x.Pipeline).WithMany(p => p.Rules).HasForeignKey(x => x.PipelineId);
            e.HasOne(x => x.LookupTable).WithMany().HasForeignKey(x => x.LookupTableId).IsRequired(false);
        });

        modelBuilder.Entity<Report>(e =>
        {
            e.ToTable("Reports", "dbo");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Codigo).IsUnique();
            e.HasOne(x => x.QueryDefinition).WithMany().HasForeignKey(x => x.QueryDefinitionId);
            e.HasOne(x => x.MappingPipeline).WithMany().HasForeignKey(x => x.MappingPipelineId);
        });
    }
}
