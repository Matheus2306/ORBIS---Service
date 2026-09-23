using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Orbis.Domain.Identity;
using Orbis.Domain.WorkOrders;

namespace Orbis.DataGenerator;

public enum DatasetLevel { Small, Medium, Large }
public enum DatasetProfile { Uniform, HotTenant }

public sealed record DatasetRecipe
{
    public const string GeneratorVersion = "2";
    public const string Issuer = "https://identity.orbis.test";
    public DatasetLevel Level { get; }
    public DatasetProfile Profile { get; }
    public int Seed { get; }
    public int TenantCount => Level switch { DatasetLevel.Small => 50, DatasetLevel.Medium => 1_000, _ => 10_000 };
    public int UserCount => Level switch { DatasetLevel.Small => 1_000, DatasetLevel.Medium => 100_000, _ => 1_000_000 };
    public int OrderCount => Level switch { DatasetLevel.Small => 10_000, DatasetLevel.Medium => 1_000_000, _ => 10_000_000 };
    public int UsersPerTenant => UserCount / TenantCount;
    public int ProviderCount => UsersPerTenant / 5;
    public int CustomerCount => UsersPerTenant - ProviderCount - 1;
    public string ConfigurationHash => Hash(FormattableString.Invariant($"orbis-dataset/{GeneratorVersion}/{Level}/{Profile}/{Seed}"));

    public DatasetRecipe(DatasetLevel level, DatasetProfile profile, int seed)
    {
        if (!Enum.IsDefined(level) || !Enum.IsDefined(profile) || seed < 0) throw new ArgumentOutOfRangeException(nameof(level));
        (Level, Profile, Seed) = (level, profile, seed);
    }

    public Guid TenantId(int tenant) => Id("tenant", tenant);
    public Guid UserId(int tenant, int slot) => Id("user", checked(tenant * UsersPerTenant + slot));
    public string Host(int tenant) => FormattableString.Invariant($"tenant-{tenant:D5}.orbis.test");
    public static string Subject(Guid userId) => userId.ToString("D");

    public Permission Permissions(int slot) => slot == 0
        ? Permission.ReadAllOrders | Permission.AssignOrders | Permission.ManageOrders
        : slot <= CustomerCount ? Permission.ReadOwnOrders | Permission.CreateOrders | Permission.CancelOwnOrders
        : Permission.ExecuteAssignedOrders;

    public int OrdersForTenant(int tenant)
    {
        if (Profile == DatasetProfile.Uniform) return OrderCount / TenantCount;
        var hot = OrderCount * 3 / 10;
        if (tenant == 0) return hot;
        var rest = OrderCount - hot;
        return rest / (TenantCount - 1) + (tenant <= rest % (TenantCount - 1) ? 1 : 0);
    }

    public IEnumerable<SyntheticOrder> Orders()
    {
        var index = 0;
        var epoch = new DateTimeOffset(2024, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var seconds = (long)(epoch.AddYears(2) - epoch).TotalSeconds - 300;
        for (var tenant = 0; tenant < TenantCount; tenant++)
        {
            for (var local = 0; local < OrdersForTenant(tenant); local++, index++)
            {
                var digest = Digest("order", index);
                var at = epoch.AddSeconds(BinaryPrimitives.ReadUInt32BigEndian(digest) % seconds);
                var slot = local % 20;
                var status = slot switch
                {
                    < 10 => WorkOrderStatus.Requested,
                    < 12 => WorkOrderStatus.Assigned,
                    < 14 => WorkOrderStatus.Accepted,
                    < 16 => WorkOrderStatus.InProgress,
                    < 19 => WorkOrderStatus.Completed,
                    _ => WorkOrderStatus.Cancelled
                };
                // UUIDv7 preserva a localidade temporal usada pelo domínio, com entropia reproduzível.
                var milliseconds = at.ToUnixTimeMilliseconds();
                for (var b = 5; b >= 0; b--) { digest[b] = (byte)milliseconds; milliseconds >>= 8; }
                digest[6] = (byte)((digest[6] & 15) | 0x70);
                digest[8] = (byte)((digest[8] & 63) | 0x80);
                yield return new(index, TenantId(tenant), new Guid(digest.AsSpan(0, 16), bigEndian: true),
                    UserId(tenant, 1 + local % CustomerCount), UserId(tenant, 0),
                    status is WorkOrderStatus.Requested or WorkOrderStatus.Cancelled ? null : UserId(tenant, CustomerCount + 1 + local % ProviderCount),
                    FormattableString.Invariant($"Synthetic service request {index:D8}. ") + new string('x', 96 + local % 128),
                    status, status == WorkOrderStatus.Cancelled ? 2 : (int)status + 1, at);
            }
        }
    }

    public Guid Id(string kind, int index, int step = 0)
    {
        var bytes = Digest(kind, index, step);
        bytes[6] = (byte)((bytes[6] & 15) | 0x80);
        bytes[8] = (byte)((bytes[8] & 63) | 0x80);
        return new Guid(bytes.AsSpan(0, 16), bigEndian: true);
    }

    private byte[] Digest(string kind, int index, int step = 0) => SHA256.HashData(Encoding.UTF8.GetBytes(
        FormattableString.Invariant($"orbis-dataset/{GeneratorVersion}/{Seed}/{kind}/{index}/{step}")));

    public static string Hash(string canonical) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
}

public sealed record SyntheticOrder(int Index, Guid TenantId, Guid Id, Guid CustomerId, Guid DispatcherId, Guid? ProviderId,
    string Description, WorkOrderStatus Status, long Version, DateTimeOffset CreatedAt)
{
    public IEnumerable<SyntheticTransition> Transitions()
    {
        if (Status == WorkOrderStatus.Cancelled)
        {
            yield return new(WorkOrderAction.Cancel, CustomerId, WorkOrderStatus.Cancelled, 2, CreatedAt.AddMinutes(1));
            yield break;
        }
        for (var step = 1; step < Version; step++)
            yield return new((WorkOrderAction)step, step == 1 ? DispatcherId : ProviderId!.Value,
                (WorkOrderStatus)step, step + 1, CreatedAt.AddMinutes(step));
    }

    public string TransitionFingerprint(SyntheticTransition transition) => DatasetRecipe.Hash(string.Join('\n',
        "work-order.transition.v1", transition.Action.ToString(), Id.ToString("D"),
        (transition.Version - 1).ToString(CultureInfo.InvariantCulture), transition.Action == WorkOrderAction.Assign ? ProviderId?.ToString("D") : string.Empty));
}

public sealed record SyntheticTransition(WorkOrderAction Action, Guid ActorId, WorkOrderStatus Status, long Version, DateTimeOffset At);
