using HomeMonitoring.Shared.Models;
using HomeMonitoring.Shared.Models.PhilipsHue;
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
    public DbSet<HueLight> HueLights { get; set; }
    public DbSet<HueLightReading> HueLightReadings { get; set; }
    public DbSet<HueBridgeConfiguration> HueBridgeConfigurations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Existing Device configuration
        modelBuilder.Entity<Device>()
            .HasIndex(d => d.SerialNumber)
            .IsUnique();

        modelBuilder.Entity<Device>()
            .Property(d => d.ProductType)
            .HasConversion<string>();

        // Existing EnergyReading configuration
        modelBuilder.Entity<EnergyReading>()
            .HasOne(e => e.Device)
            .WithMany()
            .HasForeignKey(e => e.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<EnergyReading>()
            .HasIndex(e => new { e.DeviceId, e.Timestamp });

        // HueLight configuration
        modelBuilder.Entity<HueLight>()
            .HasIndex(h => new { h.HueId, h.BridgeIpAddress })
            .IsUnique();

        // HueLightReading configuration
        modelBuilder.Entity<HueLightReading>()
            .HasOne(r => r.HueLight)
            .WithMany(l => l.Readings)
            .HasForeignKey(r => r.HueLightId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<HueLightReading>()
            .HasIndex(r => new { r.HueLightId, r.Timestamp });

        // HueBridgeConfiguration
        modelBuilder.Entity<HueBridgeConfiguration>()
            .HasIndex(b => b.BridgeId)
            .IsUnique();
    }
}