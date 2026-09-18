using NpgsqlTypes;
using Orbis.Domain.Identity;
using Orbis.Domain.WorkOrders;
using static NpgsqlTypes.NpgsqlDbType;

namespace Orbis.DataGenerator;

internal sealed record DatasetTable(string Name, string Columns, string OrderBy, NpgsqlDbType[] Types, Func<DatasetRecipe, IEnumerable<object?[]>> Rows);

internal static class DatasetTables
{
    // Identificadores SQL são constantes internas; configuração externa nunca compõe SQL.
    internal static readonly DatasetTable[] All =
    [
        new("directory.tenants", "id,name,is_active", "id", [Uuid, Text, NpgsqlDbType.Boolean], Tenants),
        new("directory.tenant_domains", "host,tenant_id,is_verified", "host", [Text, Uuid, NpgsqlDbType.Boolean], Domains),
        new("directory.users", "id,is_active", "id", [Uuid, NpgsqlDbType.Boolean], Users),
        new("directory.external_identities", "issuer,subject,user_id", "issuer,subject", [Text, Text, Uuid], Identities),
        new("public.memberships", "tenant_id,user_id,permissions,is_active", "tenant_id,user_id", [Uuid, Uuid, Integer, NpgsqlDbType.Boolean], Memberships),
        new("public.work_orders", "tenant_id,id,customer_user_id,provider_user_id,description,status,version,created_at", "tenant_id,id", [Uuid, Uuid, Uuid, Uuid, Text, Integer, Bigint, TimestampTz], Orders),
        new("public.work_order_audit", "tenant_id,id,actor_id,order_id,action,order_version,occurred_at", "tenant_id,id", [Uuid, Uuid, Uuid, Uuid, Text, Bigint, TimestampTz], Audit),
        new("public.order_creation_receipts", "tenant_id,actor_id,key,fingerprint,order_id,created_at", "tenant_id,actor_id,key", [Uuid, Uuid, Uuid, Text, Uuid, TimestampTz], CreationReceipts),
        new("public.order_transition_receipts", "tenant_id,actor_id,action,key,fingerprint,order_id,status,version,created_at", "tenant_id,actor_id,action,key", [Uuid, Uuid, Integer, Uuid, Text, Uuid, Integer, Bigint, TimestampTz], TransitionReceipts)
    ];

    private static IEnumerable<object?[]> Tenants(DatasetRecipe recipe)
    {
        for (var t = 0; t < recipe.TenantCount; t++) yield return [recipe.TenantId(t), FormattableString.Invariant($"Synthetic organization {t:D5}"), true];
    }

    private static IEnumerable<object?[]> Domains(DatasetRecipe recipe)
    {
        for (var t = 0; t < recipe.TenantCount; t++) yield return [recipe.Host(t), recipe.TenantId(t), true];
    }

    private static IEnumerable<object?[]> Users(DatasetRecipe recipe)
    {
        for (var t = 0; t < recipe.TenantCount; t++)
            for (var u = 0; u < recipe.UsersPerTenant; u++) yield return [recipe.UserId(t, u), true];
    }

    private static IEnumerable<object?[]> Identities(DatasetRecipe recipe)
    {
        for (var t = 0; t < recipe.TenantCount; t++)
            for (var u = 0; u < recipe.UsersPerTenant; u++)
            {
                var user = recipe.UserId(t, u);
                yield return [DatasetRecipe.Issuer, DatasetRecipe.Subject(user), user];
            }
    }

    private static IEnumerable<object?[]> Memberships(DatasetRecipe recipe)
    {
        for (var t = 0; t < recipe.TenantCount; t++)
        {
            for (var u = 0; u < recipe.UsersPerTenant; u++) yield return [recipe.TenantId(t), recipe.UserId(t, u), (int)recipe.Permissions(u), true];
            // Um prestador também é cliente do tenant seguinte, sem duplicar sua identidade global.
            yield return [recipe.TenantId((t + 1) % recipe.TenantCount), recipe.UserId(t, recipe.UsersPerTenant - 1),
                (int)(Permission.ReadOwnOrders | Permission.CreateOrders | Permission.CancelOwnOrders), true];
        }
    }

    private static IEnumerable<object?[]> Orders(DatasetRecipe recipe)
    {
        foreach (var o in recipe.Orders()) yield return [o.TenantId, o.Id, o.CustomerId, o.ProviderId, o.Description, (int)o.Status, o.Version, o.CreatedAt];
    }

    private static IEnumerable<object?[]> Audit(DatasetRecipe recipe)
    {
        foreach (var o in recipe.Orders())
        {
            yield return [o.TenantId, recipe.Id("audit", o.Index), o.CustomerId, o.Id, "work-order.requested", 1L, o.CreatedAt];
            foreach (var t in o.Transitions())
            {
                var action = t.Action switch
                {
                    WorkOrderAction.Assign => "assigned",
                    WorkOrderAction.Accept => "accepted",
                    WorkOrderAction.Start => "started",
                    WorkOrderAction.Complete => "completed",
                    WorkOrderAction.Cancel => "cancelled",
                    _ => throw new InvalidOperationException()
                };
                yield return [o.TenantId, recipe.Id("audit", o.Index, (int)t.Action), t.ActorId, o.Id, "work-order." + action, t.Version, t.At];
            }
        }
    }

    private static IEnumerable<object?[]> CreationReceipts(DatasetRecipe recipe)
    {
        foreach (var o in recipe.Orders()) yield return [o.TenantId, o.CustomerId, recipe.Id("create-key", o.Index),
            DatasetRecipe.Hash("work-order.create.v1\n" + o.Description), o.Id, o.CreatedAt];
    }

    private static IEnumerable<object?[]> TransitionReceipts(DatasetRecipe recipe)
    {
        foreach (var o in recipe.Orders())
            foreach (var t in o.Transitions()) yield return [o.TenantId, t.ActorId, (int)t.Action, recipe.Id("transition-key", o.Index, (int)t.Action),
                o.TransitionFingerprint(t), o.Id, (int)t.Status, t.Version, t.At];
    }
}
