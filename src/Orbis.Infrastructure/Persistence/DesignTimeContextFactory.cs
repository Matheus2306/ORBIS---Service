using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Orbis.Application.Tenancy;

namespace Orbis.Infrastructure.Persistence;

public sealed class DesignTimeContextFactory : IDesignTimeDbContextFactory<TenantDbContext>
{
    public TenantDbContext CreateDbContext(string[] args)
    {
        // Scaffolding usa somente o modelo; migrations aplicadas exigem conexão explícita do operador.
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=orbis_design_only;Timeout=2;Command Timeout=5")
            .Options;
        return new TenantDbContext(options, new TenantScope(Guid.Parse("00000000-0000-0000-0000-000000000001")));
    }
}
