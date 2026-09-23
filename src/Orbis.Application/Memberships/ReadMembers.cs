using System.Buffers.Text;
using System.Text.Json;
using Orbis.Application.Tenancy;

namespace Orbis.Application.Memberships;

public enum MemberStatusFilter { All, Active, Suspended }
public sealed record MemberDetails(Guid UserId, bool IsActive, IReadOnlyList<string> Permissions);
public sealed record MemberBatch(IReadOnlyList<MemberDetails> Items, bool HasMore);
public sealed record MemberPage(IReadOnlyList<MemberDetails> Items, string? NextCursor);
public enum ListMembersOutcome { Found, Invalid, Denied }
public sealed record ListMembersResult(ListMembersOutcome Outcome, MemberPage? Page = null);

public interface IMemberReader
{
    Task<MemberDetails?> FindAsync(TenantUser actor, Guid userId, CancellationToken cancellationToken);
    Task<MemberBatch?> ListAsync(TenantUser actor, int limit, Guid? afterUserId, MemberStatusFilter status, CancellationToken cancellationToken);
}

public sealed record MemberCursor(int Version, Guid TenantId, Guid ActorId, Guid AfterUserId, MemberStatusFilter Status)
{
    public string Encode() => Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(this));

    public static MemberCursor? Decode(string encoded)
    {
        if (encoded.Length is < 1 or > 512) return null;
        try
        {
            var cursor = JsonSerializer.Deserialize<MemberCursor>(Base64Url.DecodeFromChars(encoded));
            return cursor is not null && cursor.Version == 1 && cursor.TenantId != Guid.Empty && cursor.ActorId != Guid.Empty &&
                cursor.AfterUserId != Guid.Empty && Enum.IsDefined(cursor.Status) ? cursor : null;
        }
        catch (Exception exception) when (exception is JsonException or FormatException) { return null; }
    }
}

public sealed class ReadMember(ResolveTenantUser resolver, IMemberReader members)
{
    public async Task<MemberDetails?> ExecuteAsync(TenantRequest request, Guid userId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty) return null;
        var actor = await resolver.ExecuteAsync(request, cancellationToken);
        return actor is null ? null : await members.FindAsync(actor, userId, cancellationToken);
    }
}

public sealed class ListMembers(ResolveTenantUser resolver, IMemberReader members)
{
    public async Task<ListMembersResult> ExecuteAsync(TenantRequest request, int limit, string? cursor, string? status, CancellationToken cancellationToken)
    {
        if (limit is < 1 or > 100) return new(ListMembersOutcome.Invalid);
        MemberStatusFilter? filter = status switch
        {
            null or "all" => MemberStatusFilter.All,
            "active" => MemberStatusFilter.Active,
            "suspended" => MemberStatusFilter.Suspended,
            _ => null
        };
        var position = cursor is null ? null : MemberCursor.Decode(cursor);
        if (filter is null || (cursor is not null && position is null) || (position is not null && position.Status != filter))
            return new(ListMembersOutcome.Invalid);
        var actor = await resolver.ExecuteAsync(request, cancellationToken);
        // Cursor seleciona somente posição; a permissão atual será revalidada antes da consulta da página.
        if (actor is null || (position is not null && (position.TenantId != actor.TenantId || position.ActorId != actor.UserId)))
            return new(ListMembersOutcome.Denied);
        var batch = await members.ListAsync(actor, limit, position?.AfterUserId, filter.Value, cancellationToken);
        if (batch is null) return new(ListMembersOutcome.Denied);
        var next = batch.HasMore ? new MemberCursor(1, actor.TenantId, actor.UserId, batch.Items[^1].UserId, filter.Value).Encode() : null;
        return new(ListMembersOutcome.Found, new(batch.Items, next));
    }
}
