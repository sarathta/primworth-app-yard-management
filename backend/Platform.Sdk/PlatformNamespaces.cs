// Namespaces the platform has and an application might trip over. The platform declares
// Primworth_API.Models.System, Primworth_API.Queries.System and Primworth_API.ViewModels.System, so inside
// an app's Primworth_API.Models.* / Queries.* / ViewModels.* code a fully qualified "System.Globalization..."
// resolves to the platform's namespace and fails to compile after integrate.ps1. Declaring them here makes
// the same mistake fail in the kit, where it is cheap: write "using System.Globalization;" at the top instead.
namespace Primworth_API.Models.System { public static class PlatformNamespace { } }
namespace Primworth_API.Queries.System { public static class PlatformNamespace { } }
namespace Primworth_API.ViewModels.System { public static class PlatformNamespace { } }
