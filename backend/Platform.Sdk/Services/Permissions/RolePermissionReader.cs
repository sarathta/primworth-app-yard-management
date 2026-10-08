using System.Security.Claims;

namespace Primworth_API.Services.Permissions
{
    /// <summary>
    /// The permission codes a role carries — for the checks an attribute cannot express, e.g. "an
    /// administrator sees every roll shop, everyone else only their own". Endpoints still declare
    /// their codes with [RequiresPermission]; this is for decisions inside an action.
    ///
    /// In the platform the answer comes from the role's permission mapping (super admin: null). In the
    /// dev host it comes from <c>Dev:Permissions</c> ("*" behaves as super admin).
    /// </summary>
    public interface IRolePermissionReader
    {
        bool Enforcing { get; }

        /// <summary>
        /// The permission codes a role carries. Null means "let it through" — the role is a super
        /// admin, or the lookup could not be made.
        /// </summary>
        Task<IReadOnlySet<string>?> GetForRoleAsync(long roleId, CancellationToken ct = default);
    }

    public static class RolePermissionReaderExtensions
    {
        /// <summary>Whether the signed-in user's role holds <paramref name="code"/>, with the platform's rules for failing.</summary>
        public static async Task<bool> HasAsync(
            this IRolePermissionReader reader, ClaimsPrincipal user, string code, CancellationToken ct = default)
        {
            if (!reader.Enforcing) return true;

            if (!long.TryParse(user.FindFirst(ClaimTypes.Role)?.Value, out var roleId))
                return false;

            var granted = await reader.GetForRoleAsync(roleId, ct);

            // Null is super admin or an unreadable lookup; empty is an unconfigured role.
            return granted is null || granted.Count == 0 || granted.Contains(code);
        }
    }
}
