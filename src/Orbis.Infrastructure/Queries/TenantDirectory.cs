using Microsoft.EntityFrameworkCore;
using Orbis.Application.WorkOrders;
using Orbis.Infrastructure.Persistence;

namespace Orbis.Infrastructure.Queries;

public sealed class TenantDirectory(DirectoryDbContext database) : ITenantDirectory
{
    public Task<TenantUser?> ResolveAsync(string host, string issuer, string subject, CancellationToken cancellationToken) =>
        (from domain in database.Domains.AsNoTracking()
         join tenant in database.Tenants on domain.TenantId equals tenant.Id
         from identity in database.Identities.Where(x => x.Issuer == issuer && x.Subject == subject)
         join user in database.Users on identity.UserId equals user.Id
         where domain.Host == host && domain.IsVerified && tenant.IsActive && user.IsActive
         select new TenantUser(tenant.Id, user.Id)).SingleOrDefaultAsync(cancellationToken);
}
