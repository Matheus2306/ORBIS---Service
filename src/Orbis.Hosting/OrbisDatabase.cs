using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Orbis.Infrastructure.Persistence;

namespace Orbis.Hosting;

public static class OrbisDatabase
{
    public static void AddOrbisDatabase(this IServiceCollection services, string connectionName, string poolName, int maximumPoolSize)
    {
        services.AddSingleton(provider =>
        {
            var connection = provider.GetRequiredService<IConfiguration>().GetConnectionString(connectionName);
            if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("Database connection is required.");
            var settings = new NpgsqlConnectionStringBuilder(connection);
            var environment = provider.GetRequiredService<IHostEnvironment>();
            if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing") && settings.SslMode != SslMode.VerifyFull)
                throw new InvalidOperationException("Database TLS with full certificate verification is required outside Development and Testing.");
            settings.MaxPoolSize = Math.Min(settings.MaxPoolSize, maximumPoolSize);
            settings.Timeout = 5;
            settings.CommandTimeout = 5;
            // Os nomes vêm do host, não da configuração/tenant; a connection string não vira label de métrica.
            return new NpgsqlDataSourceBuilder(settings.ConnectionString) { Name = poolName }.Build();
        });
        services.AddDbContext<DirectoryDbContext>((provider, options) =>
            options.UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>(), pg =>
                pg.MigrationsHistoryTable("__DirectoryMigrationsHistory", "directory")));
        services.AddSingleton(provider => new DbContextOptionsBuilder<TenantDbContext>()
            .UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>()).Options);
    }
}
