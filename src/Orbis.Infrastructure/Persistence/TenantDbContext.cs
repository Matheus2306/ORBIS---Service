using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Orbis.Application.Tenancy;
using Orbis.Domain.Identity;
using Orbis.Domain.WorkOrders;

namespace Orbis.Infrastructure.Persistence;

public sealed class TenantDbContext(DbContextOptions<TenantDbContext> options, TenantScope scope) : DbContext(options)
{
    private Guid TenantId => scope.TenantId;
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var members = modelBuilder.Entity<Membership>();
        members.ToTable("memberships", table =>
        {
            table.HasCheckConstraint("ck_membership_ids", "tenant_id <> '00000000-0000-0000-0000-000000000000' AND user_id <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint("ck_membership_permissions", "permissions BETWEEN 0 AND 127");
        });
        members.HasKey(x => new { x.TenantId, x.UserId });
        members.Property(x => x.TenantId).HasColumnName("tenant_id").ValueGeneratedNever();
        members.Property(x => x.UserId).HasColumnName("user_id").ValueGeneratedNever();
        members.Property(x => x.Permissions).HasColumnName("permissions").HasConversion<int>();
        members.Property(x => x.IsActive).HasColumnName("is_active");
        members.HasQueryFilter(x => x.TenantId == TenantId);

        var orders = modelBuilder.Entity<WorkOrder>();
        orders.ToTable("work_orders", table =>
        {
            table.HasCheckConstraint("ck_order_status", "status BETWEEN 0 AND 5");
            table.HasCheckConstraint("ck_order_version", "version > 0");
            table.HasCheckConstraint("ck_order_description", "length(btrim(description)) > 0");
            table.HasCheckConstraint("ck_order_provider", "status NOT IN (1, 2, 3, 4) OR provider_user_id IS NOT NULL");
        });
        orders.HasKey(x => new { x.TenantId, x.Id });
        orders.Property(x => x.TenantId).HasColumnName("tenant_id").ValueGeneratedNever();
        orders.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        orders.Property(x => x.CustomerUserId).HasColumnName("customer_user_id");
        orders.Property(x => x.ProviderUserId).HasColumnName("provider_user_id");
        orders.Property(x => x.Description).HasColumnName("description").HasMaxLength(2000);
        orders.Property(x => x.Status).HasColumnName("status").HasConversion<int>();
        orders.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        orders.Property(x => x.CreatedAt).HasColumnName("created_at");
        orders.HasQueryFilter(x => x.TenantId == TenantId);
        // Incluir o tenant na FK impede que um ID válido em B seja relacionado a uma ordem de A.
        orders.HasOne<Membership>().WithMany().HasForeignKey(x => new { x.TenantId, x.CustomerUserId }).OnDelete(DeleteBehavior.Restrict);
        orders.HasOne<Membership>().WithMany().HasForeignKey(x => new { x.TenantId, x.ProviderUserId }).OnDelete(DeleteBehavior.Restrict);
        orders.HasIndex(x => new { x.TenantId, x.CreatedAt, x.Id }).IsDescending(false, true, true);
    }

    public async Task<IDbContextTransaction> BeginTenantTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            // SET LOCAL não sobrevive a commit/rollback nem contamina o próximo usuário da conexão.
            await Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('orbis.tenant_id', {TenantId.ToString()}, true)", cancellationToken);
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureTenantWrites();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnsureTenantWrites();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void EnsureTenantWrites()
    {
        if (Database.CurrentTransaction is null)
            throw new InvalidOperationException("A tenant transaction is required for writes.");
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries().Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            var tenant = entry.Property("TenantId");
            if (tenant.CurrentValue is not Guid current || current != TenantId ||
                (entry.State != EntityState.Added && (tenant.OriginalValue is not Guid original || original != TenantId)))
                throw new InvalidOperationException("Cross-tenant writes are forbidden.");
        }
    }
}
