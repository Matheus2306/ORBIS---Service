using System.Text.Json.Serialization;

namespace Orbis.Administration.Api.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MemberPermissionsBody(IReadOnlyList<string>? PermissionSet, long ExpectedVersion);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MemberStatusBody(long ExpectedVersion);
