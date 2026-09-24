using Orbis.Domain.Identity;

namespace Orbis.Application.Identity;

public static class PermissionNames
{
    public static bool TryParse(IReadOnlyList<string>? names, out Permission permissions)
    {
        permissions = Permission.None;
        if (names is null || names.Count > Enum.GetValues<Permission>().Length - 1) return false;
        foreach (var name in names)
        {
            // Enum.TryParse isolado aceitaria números, espaços e combinações não contratadas pelo DTO.
            if (!Enum.TryParse<Permission>(name, out var value) || value == Permission.None || !Enum.IsDefined(value) ||
                value.ToString() != name || (permissions & value) != 0) return false;
            permissions |= value;
        }
        return true;
    }

    // Nomes publicados são contrato da API; bits desconhecidos nunca viram permissões implícitas.
    public static IReadOnlyList<string> From(Permission permissions) => Enum.GetValues<Permission>()
        .Where(permission => permission != Permission.None && (permissions & permission) == permission)
        .Select(permission => permission.ToString()).ToArray();
}
