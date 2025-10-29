using System.CommandLine;
using LablabBean.Plugins.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LablabBean.Console.Commands;

public static class PluginsCommand
{
    public static Command Create()
    {
        var root = new Command("plugins", "Inspect and manage plugins");

        var list = new Command("list", "Discover and list plugins from configured paths");
        var pathsOption = new Option<string[]>(
            aliases: new[] { "--paths", "-p" },
            description: "Plugin search paths (overrides configuration)")
        {
            Arity = ArgumentArity.ZeroOrMore
        };
        list.AddOption(pathsOption);

        list.SetHandler(async (string[]? optPaths) =>
        {
            // Build configuration (read appsettings.json if available)
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables();
            var configuration = configBuilder.Build();

            // Build DI
            var services = new ServiceCollection();
            services.AddLogging(b =>
            {
                b.AddConsole();
                b.SetMinimumLevel(LogLevel.Information);
            });
            services.AddSingleton<IConfiguration>(configuration);
            services.AddPluginSystem(configuration);

            using var sp = services.BuildServiceProvider();

            var loader = sp.GetRequiredService<PluginLoader>();
            var admin = sp.GetRequiredService<PluginAdminService>();
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("plugins");

            // Resolve paths
            List<string> pluginPaths;
            if (optPaths != null && optPaths.Length > 0)
            {
                pluginPaths = optPaths.Select(p => Environment.ExpandEnvironmentVariables(p)).ToList();
            }
            else
            {
                var configPaths = configuration.GetSection("Plugins:Paths").Get<string[]>() ?? Array.Empty<string>();
                if (configPaths.Length == 0)
                {
                    configPaths = new[] { configuration["Plugins:DefaultPath"] ?? "plugins" };
                }
                pluginPaths = configPaths.Select(p => Environment.ExpandEnvironmentVariables(p)).ToList();
            }

            logger.LogInformation("Scanning plugin paths: {Paths}", string.Join(", ", pluginPaths));

            try
            {
                var loaded = await loader.DiscoverAndLoadAsync(pluginPaths);
                var status = await admin.GetSystemStatusAsync();

                System.Console.WriteLine($"Total: {status.TotalPlugins}, Loaded: {status.LoadedPlugins}, Failed: {status.FailedPlugins}, Health: {status.SystemHealth}");
                foreach (var p in status.Plugins.OrderBy(p => p.Name))
                {
                    var mark = p.IsLoaded ? "+" : (p.LoadError != null ? "x" : " ");
                    var health = p.Health.ToString();
                    System.Console.WriteLine($" {mark} {p.Name} v{p.Version} [{health}] {(p.LoadError ?? "")} ");
                }

                Environment.ExitCode = 0;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to discover/load plugins");
                Environment.ExitCode = 1;
            }
        }, pathsOption);

        root.AddCommand(list);
        return root;
    }
}
