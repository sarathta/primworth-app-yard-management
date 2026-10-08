using System.Reflection;
using Primworth_API.IRepository;

namespace Platform.DevHost
{
    /// <summary>
    /// Finds the referenced Apps.* assemblies and registers what they contain, by convention:
    ///   - every IApplicationProvisioner implementation, as IApplicationProvisioner;
    ///   - every concrete class, under each interface it implements from a Primworth_API.* namespace
    ///     (repositories, schema contexts);
    ///   - every IHostedService (BackgroundService) as a singleton job started with the host.
    /// This is what the platform's Program.cs does by hand, three lines per application.
    /// </summary>
    public static class AppDiscovery
    {
        /// <summary>
        /// Every Apps.*.dll next to the host. Found on disk rather than via the entry assembly's
        /// reference list, because the host deliberately references no type from an app — and the
        /// compiler drops unused references, so the list would be empty.
        /// </summary>
        public static IReadOnlyList<Assembly> LoadAppAssemblies()
        {
            var found = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.GetFiles(AppContext.BaseDirectory, "Apps.*.dll"))
            {
                var name = AssemblyName.GetAssemblyName(file);
                if (name.Name != null) found[name.Name] = Assembly.Load(name);
            }
            return found.Values.ToList();
        }

        public static void RegisterServices(IServiceCollection services, Assembly assembly, ILogger logger)
        {
            foreach (var type in assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract && t.IsPublic))
            {
                if (typeof(IApplicationProvisioner).IsAssignableFrom(type))
                {
                    services.AddScoped(typeof(IApplicationProvisioner), type);
                    logger.LogInformation("[DevHost] provisioner {Type}", type.FullName);
                }

                // A background job: one instance, started with the host, and also resolvable by its own
                // type so a controller can report its status (manifest backend.hostedServices).
                if (typeof(IHostedService).IsAssignableFrom(type))
                {
                    services.AddSingleton(type);
                    services.AddSingleton(typeof(IHostedService), sp => sp.GetRequiredService(type));
                    logger.LogInformation("[DevHost] job      {Type}", type.FullName);
                    continue;
                }

                foreach (var iface in type.GetInterfaces())
                {
                    if (iface == typeof(IApplicationProvisioner)) continue;
                    if (iface.Namespace == null || !iface.Namespace.StartsWith("Primworth_API", StringComparison.Ordinal)) continue;
                    services.AddScoped(iface, type);
                    logger.LogInformation("[DevHost] service  {Iface} -> {Impl}", iface.Name, type.Name);
                }
            }
        }
    }
}
