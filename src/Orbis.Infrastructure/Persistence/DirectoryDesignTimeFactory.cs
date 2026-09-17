using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Orbis.Infrastructure.Persistence;

public sealed class DirectoryDesignTimeFactory : IDesignTimeDbContextFactory<DirectoryDbContext>
{
    public DirectoryDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<DirectoryDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=orbis_design_only;Timeout=2;Command Timeout=5",
                options => options.MigrationsHistoryTable("__DirectoryMigrationsHistory", "directory"))
            .Options);
}
