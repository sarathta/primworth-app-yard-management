using Primworth_API.Services.Permissions;

namespace Platform.DevHost
{
    /// <summary>
    /// The dev user's permissions for checks made inside an action, from <c>Dev:Permissions</c> —
    /// the same list <see cref="DevPermissionFilter"/> enforces. "*" answers null, as a super admin
    /// does in the platform.
    /// </summary>
    public class DevRolePermissionReader : IRolePermissionReader
    {
        private readonly DevOptions _dev;

        public DevRolePermissionReader(DevOptions dev) => _dev = dev;

        public bool Enforcing => true;

        public Task<IReadOnlySet<string>?> GetForRoleAsync(long roleId, CancellationToken ct = default)
        {
            IReadOnlySet<string>? codes = _dev.Permissions.Contains("*")
                ? null
                : _dev.Permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(codes);
        }
    }
}
