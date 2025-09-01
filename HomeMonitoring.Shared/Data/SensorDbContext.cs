using HomeMonitoring.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace HomeMonitoring.Shared.Data;

public class SensorDbContext : DbContext
{
    public SensorDbContext(DbContextOptions<SensorDbContext> options)
        : base(options)
    {
    }

    public DbSet<Device> Devices { get; set; }
    public DbSet<EnergyReading> EnergyReadings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure the Device entity
        modelBuilder.Entity<Device>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.IpAddress).IsRequired().HasMaxLength(45);
            entity.Property(e => e.SerialNumber).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ProductTypeRaw).HasMaxLength(50);
            entity.HasIndex(e => e.SerialNumber).IsUnique();

            // Store ProductType as string in database
            entity.Property(e => e.ProductType)
                .HasConversion<string>()
                .HasMaxLength(50);
        });

        // Configure the EnergyReading entity
        modelBuilder.Entity<EnergyReading>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Device)
                .WithMany()
                .HasForeignKey(e => e.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.DeviceId, e.Timestamp });
        });
    }
}