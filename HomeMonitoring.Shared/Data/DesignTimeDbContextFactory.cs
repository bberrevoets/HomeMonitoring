// Copyright (c) 2025 Bert Berrevoets
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HomeMonitoring.Shared.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SensorDbContext>
{
    public SensorDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SensorDbContext>();

        // Use a hardcoded connection string for migrations
        optionsBuilder.UseSqlServer("sensorsdb");

        return new SensorDbContext(optionsBuilder.Options);
    }
}