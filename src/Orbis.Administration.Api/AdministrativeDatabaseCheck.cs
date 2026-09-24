using Npgsql;
using Orbis.Hosting;
using Orbis.Infrastructure.Persistence;

namespace Orbis.Administration.Api;

public sealed class AdministrativeDatabaseCheck(NpgsqlDataSource source, TimeProvider clock) : DatabaseReadinessCheck(clock)
{
    protected override Task<bool> IsSafeAsync(CancellationToken cancellationToken) =>
        DatabasePrivileges.IsSafeAsync(source, DatabaseAccessProfile.MembershipAdministration, cancellationToken);
}
