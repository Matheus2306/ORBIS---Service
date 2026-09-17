using System.Buffers.Text;
using System.Text.Json;
using Orbis.Application.Tenancy;

namespace Orbis.Application.WorkOrders;

public sealed record OrderPosition(DateTimeOffset CreatedAt, Guid Id);
public sealed record OrderBatch(IReadOnlyList<OrderDetails> Items, bool HasMore);
public sealed record OrderPage(IReadOnlyList<OrderDetails> Items, string? NextCursor);
public enum ListOrdersOutcome { Found, Invalid, Denied }
public sealed record ListOrdersResult(ListOrdersOutcome Outcome, OrderPage? Page = null);

// Cursor é somente uma posição, nunca uma credencial; tenant/ator são cruzados com a identidade atual.
public sealed record OrderCursor(int Version, Guid TenantId, Guid UserId, long UtcTicks, Guid Id)
{
    public string Encode() => Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(this));

    public static OrderCursor? Decode(string encoded)
    {
        if (encoded.Length is < 1 or > 512) return null;
        try
        {
            var cursor = JsonSerializer.Deserialize<OrderCursor>(Base64Url.DecodeFromChars(encoded));
            return cursor is not null && cursor.Version == 1 && cursor.TenantId != Guid.Empty && cursor.UserId != Guid.Empty &&
                cursor.Id != Guid.Empty && cursor.UtcTicks >= DateTimeOffset.MinValue.UtcTicks &&
                cursor.UtcTicks <= DateTimeOffset.MaxValue.UtcTicks && cursor.UtcTicks % 10 == 0 ? cursor : null;
        }
        catch (Exception exception) when (exception is FormatException or JsonException) { return null; }
    }
}

public sealed class ListWorkOrders(ResolveTenantUser resolver, IWorkOrderReader orders)
{
    public async Task<ListOrdersResult> ExecuteAsync(TenantRequest request, int limit, string? cursor, CancellationToken cancellationToken)
    {
        if (limit is < 1 or > 100) return new(ListOrdersOutcome.Invalid);
        var decoded = cursor is null ? null : OrderCursor.Decode(cursor);
        if (cursor is not null && decoded is null) return new(ListOrdersOutcome.Invalid);
        var user = await resolver.ExecuteAsync(request, cancellationToken);
        if (user is null || (decoded is not null && (decoded.TenantId != user.TenantId || decoded.UserId != user.UserId)))
            return new(ListOrdersOutcome.Denied);
        var position = decoded is null ? null : new OrderPosition(new DateTimeOffset(decoded.UtcTicks, TimeSpan.Zero), decoded.Id);
        var batch = await orders.ListAsync(user, limit, position, cancellationToken);
        if (batch is null) return new(ListOrdersOutcome.Denied);
        var last = batch.HasMore ? batch.Items[^1] : null;
        var next = last is null ? null : new OrderCursor(1, user.TenantId, user.UserId, last.CreatedAt.UtcTicks, last.Id).Encode();
        return new(ListOrdersOutcome.Found, new(batch.Items, next));
    }
}
