using Microsoft.EntityFrameworkCore;
using Orbis.Application.Tenancy;
using Orbis.Domain.Identity;
using Orbis.Infrastructure.Persistence;

namespace Orbis.Infrastructure.Queries;

public sealed class CurrentContextReader(DbContextOptions<TenantDbContext> options, DirectoryDbContext directory) : ICurrentContextReader
{
    public async Task<CurrentContext?> FindAsync(TenantUser user, CancellationToken cancellationToken)
    {
        Permission? permissions;
        await using (var database = new TenantDbContext(options, new TenantScope(user.TenantId)))
        {
            await using var transaction = await database.BeginTenantTransactionAsync(cancellationToken);
            permissions = await database.Memberships.AsNoTracking()
                .Where(member => member.UserId == user.UserId && member.IsActive)
                .Select(member => (Permission?)member.Permissions).SingleOrDefaultAsync(cancellationToken);
        }
        if (permissions is null) return null;
        // Nome da organização só é projetado após comprovar vínculo no contexto protegido por RLS.
        // Liberar a conexão tenant antes da consulta ao diretório evita reter duas posições no pool por request.
        var name = await directory.Tenants.AsNoTracking().Where(tenant => tenant.Id == user.TenantId && tenant.IsActive)
            .Select(tenant => tenant.Name).SingleOrDefaultAsync(cancellationToken);
        return name is null ? null : new(user.TenantId, user.UserId, name, permissions.Value);
    }
}
