namespace Platform.DevHost
{
    /// <summary>The "Dev" section of appsettings.json.</summary>
    public class DevOptions
    {
        public string JwtKey { get; set; } = "primworth-app-starter-dev-key-not-for-production-0123456789abcdef";
        public DevUser User { get; set; } = new();

        /// <summary>Permission codes the dev user holds. "*" grants everything (super admin).</summary>
        public string[] Permissions { get; set; } = { "*" };

        /// <summary>
        /// Projects the dev user is mapped to, as the platform's User Management maps users:
        /// "7" is all of project 7, "7/3" only its sub-project 3, "*" everywhere (super admin).
        /// Narrow it to see how your app answers a user from another plant (403). Unset means "*".
        /// No default here on purpose: the configuration binder appends to an array that already
        /// has values, so a default of "*" would survive any list set in appsettings.
        /// </summary>
        public string[]? Projects { get; set; }

        public string[] CorsOrigins { get; set; } = { "http://localhost:5175" };
        public bool ProvisionOnStartup { get; set; } = true;

        /// <summary>What the login page, sidebar and header show. The logo is served by the dev shell.</summary>
        public DevPlatform Platform { get; set; } = new();
    }

    public class DevPlatform
    {
        public string Name { get; set; } = "MES Platform";
        public string PoweredBy { get; set; } = "powered by primworth";
        public string Logo { get; set; } = "/logo.png";
        public string OemName { get; set; } = "Primworth Technologies";
        public string CustomerName { get; set; } = "Dev Steel Works";
    }

    public class DevUser
    {
        public long Id { get; set; } = 1;
        public string Email { get; set; } = "dev@local";
        public string Password { get; set; } = "dev";
        public string FirstName { get; set; } = "Dev";
        public string LastName { get; set; } = "User";
    }
}
