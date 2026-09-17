using Microsoft.EntityFrameworkCore;
using Orbis.Domain.Identity;
using Orbis.Domain.Tenancy;

namespace Orbis.Infrastructure.Persistence;

public sealed class DirectoryDbContext(DbContextOptions<DirectoryDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantDomain> Domains => Set<TenantDomain>();
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<ExternalIdentity> Identities => Set<ExternalIdentity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("directory");
        var tenant = modelBuilder.Entity<Tenant>();
        tenant.ToTable("tenants", table => table.HasCheckConstraint("ck_tenant_id", "id <> '00000000-0000-0000-0000-000000000000'"));
        tenant.HasKey(x => x.Id);
        tenant.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        tenant.Property(x => x.Name).HasColumnName("name").HasMaxLength(120);
        tenant.Property(x => x.IsActive).HasColumnName("is_active");

        var domain = modelBuilder.Entity<TenantDomain>();
        domain.ToTable("tenant_domains");
        domain.HasKey(x => x.Host);
        domain.Property(x => x.Host).HasColumnName("host").HasMaxLength(253).UseCollation("C");
        domain.Property(x => x.TenantId).HasColumnName("tenant_id");
        domain.Property(x => x.IsVerified).HasColumnName("is_verified");
        domain.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);

        var user = modelBuilder.Entity<UserAccount>();
        user.ToTable("users", table => table.HasCheckConstraint("ck_user_id", "id <> '00000000-0000-0000-0000-000000000000'"));
        user.HasKey(x => x.Id);
        user.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        user.Property(x => x.IsActive).HasColumnName("is_active");

        var identity = modelBuilder.Entity<ExternalIdentity>();
        identity.ToTable("external_identities");
        identity.HasKey(x => new { x.Issuer, x.Subject });
        identity.Property(x => x.Issuer).HasColumnName("issuer").HasMaxLength(512).UseCollation("C");
        identity.Property(x => x.Subject).HasColumnName("subject").HasMaxLength(256).UseCollation("C");
        identity.Property(x => x.UserId).HasColumnName("user_id");
        identity.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
