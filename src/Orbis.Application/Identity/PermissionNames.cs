using Orbis.Domain.Identity;

namespace Orbis.Application.Identity;

public static class PermissionNames
{
    // Nomes publicados são contrato da API; bits desconhecidos nunca viram permissões implícitas.
    public static IReadOnlyList<string> From(Permission permissions) => Enum.GetValues<Permission>()
        .Where(permission => permission != Permission.None && (permissions & permission) == permission)
        .Select(permission => permission.ToString()).ToArray();
}
