using Homologador.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Homologador.AzureApi.Data;

public class AzureDbContext : DbContext
{
    public AzureDbContext(DbContextOptions<AzureDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserReport> UserReports => Set<UserReport>();
    public DbSet<RegistroRecepcion> RegistrosRecepcion => Set<RegistroRecepcion>();
    public DbSet<RegistroCalidad> RegistrosCalidad => Set<RegistroCalidad>();
    public DbSet<RegistroProduccion> RegistrosProduccion => Set<RegistroProduccion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("Users", "dbo");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.Codigo).IsUnique();
        });

        modelBuilder.Entity<UserReport>(e =>
        {
            e.ToTable("UserReports", "dbo");
            e.HasKey(x => new { x.UserId, x.ReportId });
            e.HasOne(x => x.User).WithMany(u => u.UserReports).HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<RegistroRecepcion>(e =>
        {
            e.ToTable("RegistrosRecepcion", "dbo");
            e.HasKey(x => new { x.ReportId, x.Lote });
            e.Property(x => x.Kg).HasPrecision(12, 3);
        });

        modelBuilder.Entity<RegistroCalidad>(e =>
        {
            e.ToTable("RegistrosCalidad", "dbo");
            e.HasKey(x => new { x.ReportId, x.Lote });
        });

        modelBuilder.Entity<RegistroProduccion>(e =>
        {
            e.ToTable("RegistrosProduccion", "dbo");
            e.HasKey(x => new { x.ReportId, x.LoteProceso, x.CodigoPallet });
        });
    }
}
