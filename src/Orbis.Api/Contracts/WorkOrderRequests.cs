using System.Text.Json.Serialization;

namespace Orbis.Api.Contracts;

// Campos de autoridade adicionais são rejeitados, inclusive TenantId/CustomerId/Status no payload.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RequestOrderBody(string? Description);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TransitionOrderBody(long ExpectedVersion, Guid? ProviderUserId = null);
