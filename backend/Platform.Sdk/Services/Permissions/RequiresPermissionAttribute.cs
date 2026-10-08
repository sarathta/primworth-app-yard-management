namespace Primworth_API.Services.Permissions
{
    /// <summary>
    /// Declares the permission code(s) an endpoint needs. Put it on a controller for the whole
    /// controller, on an action to add a stricter requirement for that action.
    ///
    /// In the platform this attribute is enforced against the user's role. In the dev host it is
    /// enforced against <c>Dev:Permissions</c> in appsettings ("*" grants everything).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public sealed class RequiresPermissionAttribute : Attribute
    {
        public string[] Codes { get; }

        public RequiresPermissionAttribute(params string[] codes) => Codes = codes;
    }
}
