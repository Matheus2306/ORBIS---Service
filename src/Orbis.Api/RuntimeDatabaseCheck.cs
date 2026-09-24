using Npgsql;
using Orbis.Infrastructure.Persistence;

namespace Orbis.Api;

public sealed class RuntimeDatabaseCheck(NpgsqlDataSource dataSource, TimeProvider timeProvider) : DatabaseReadinessCheck(timeProvider)
{
    protected override Task<bool> IsSafeAsync(CancellationToken cancellationToken) =>
        DatabasePrivileges.IsSafeAsync(dataSource, DatabaseAccessProfile.Service, cancellationToken);
}
