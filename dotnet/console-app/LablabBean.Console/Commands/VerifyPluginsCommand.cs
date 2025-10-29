using System.CommandLine;
using LablabBean.Plugins.Core;
using LablabBean.Plugins.Contracts;
using LablabBean.Contracts.Diagnostic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace LablabBean.Console.Commands;

public static class VerifyPluginsCommand
{
    public static Command Create()
    {
        var cmd = new Command("verify", "Load plugins, probe known contracts at runtime, and export plugin-health.json");

        var pathsOption = new Option<string[]>(
            aliases: new[] { "--paths", "-p" },
            description: "Plugin search paths (defaults to config Plugins:Paths)")
        { Arity = ArgumentArity.ZeroOrMore };

        var includeOption = new Option<string[]>(
            aliases: new[] { "--include" },
            description: "Only verify plugins with these IDs (comma or repeat)"
        )
        { Arity = ArgumentArity.ZeroOrMore };

        var excludeOption = new Option<string[]>(
            aliases: new[] { "--exclude" },
            description: "Skip plugins with these IDs (comma or repeat)"
        )
        { Arity = ArgumentArity.ZeroOrMore };

        var outputOption = new Option<FileInfo>(
            aliases: new[] { "--output", "-o" },
            description: "Path to write plugin-health.json")
        { IsRequired = true };

        var timeoutOption = new Option<int>(
            aliases: new[] { "--timeout-ms" },
            getDefaultValue: () => 2000,
            description: "Timeout for contract probes (ms)");

        cmd.AddOption(pathsOption);
        cmd.AddOption(outputOption);
        cmd.AddOption(timeoutOption);
        cmd.AddOption(includeOption);
        cmd.AddOption(excludeOption);

        cmd.SetHandler(async (string[]? optPaths, FileInfo output, int timeoutMs, string[]? include, string[]? exclude) =>
        {
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables();
            var configuration = configBuilder.Build();

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
            var registry = (IRegistry)sp.GetRequiredService<ServiceRegistry>();
            var admin = sp.GetRequiredService<PluginAdminService>();
            var metrics = sp.GetRequiredService<PluginSystemMetrics>();
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("verify");

            // Resolve plugin paths
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

            logger.LogInformation("Verifying plugins from: {Paths}", string.Join(", ", pluginPaths));

            // Pre-scan plugin folders to map assembly file -> plugin name (for per-contract probe attribution)
            var assemblyToPlugin = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var root in pluginPaths)
            {
                if (!Directory.Exists(root)) continue;
                foreach (var pluginDir in Directory.GetDirectories(root))
                {
                    var manifestPath = Path.Combine(pluginDir, "plugin.json");
                    if (!File.Exists(manifestPath)) continue;
                    try
                    {
                        var manifest = LablabBean.Plugins.Core.ManifestParser.ParseFile(manifestPath);
                        // Map the manifest-declared entry assembly (if present) and any dlls in folder
                        if (!string.IsNullOrWhiteSpace(manifest.EntryAssembly))
                        {
                            assemblyToPlugin[manifest.EntryAssembly.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                                ? manifest.EntryAssembly
                                : manifest.EntryAssembly + ".dll"] = manifest.Name;
                        }
                        foreach (var dll in Directory.GetFiles(pluginDir, "*.dll"))
                        {
                            assemblyToPlugin[Path.GetFileName(dll)] = manifest.Name;
                        }
                    }
                    catch { /* ignore malformed manifest; loader logs it later */ }
                }
            }

            // Build candidate plugin directories (id -> dir) and apply include/exclude filters
            var candidates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var root in pluginPaths)
            {
                if (!Directory.Exists(root)) continue;
                foreach (var pluginDir in Directory.GetDirectories(root))
                {
                    var manifestPath = Path.Combine(pluginDir, "plugin.json");
                    if (!File.Exists(manifestPath)) continue;
                    try
                    {
                        var manifest = LablabBean.Plugins.Core.ManifestParser.ParseFile(manifestPath);
                        candidates[manifest.Id] = pluginDir;
                    }
                    catch { }
                }
                // Also consider the root itself if it is a plugin folder
                var selfManifest = Path.Combine(root, "plugin.json");
                if (File.Exists(selfManifest))
                {
                    try
                    {
                        var manifest = LablabBean.Plugins.Core.ManifestParser.ParseFile(selfManifest);
                        candidates[manifest.Id] = root;
                    }
                    catch { }
                }
            }

            IEnumerable<string> selectedDirs = candidates.Values;
            if (include != null && include.Length > 0)
            {
                var allow = new HashSet<string>(include.SelectMany(s => s.Split(',', StringSplitOptions.RemoveEmptyEntries)).Select(s => s.Trim()), StringComparer.OrdinalIgnoreCase);
                selectedDirs = candidates.Where(kv => allow.Contains(kv.Key)).Select(kv => kv.Value);
            }
            if (exclude != null && exclude.Length > 0)
            {
                var block = new HashSet<string>(exclude.SelectMany(s => s.Split(',', StringSplitOptions.RemoveEmptyEntries)).Select(s => s.Trim()), StringComparer.OrdinalIgnoreCase);
                selectedDirs = candidates.Where(kv => !block.Contains(kv.Key)).Select(kv => kv.Value);
            }

            // Discover and load only the selected plugin directories (supported by loader self-manifest check)
            await loader.DiscoverAndLoadAsync(selectedDirs).ConfigureAwait(false);

            // Contract-specific probes (currently: IDiagnosticProvider)
            var diagProviders = registry.GetAll<IDiagnosticProvider>()?.ToList() ?? new List<IDiagnosticProvider>();
            var contractProbes = new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
            var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));
            foreach (var p in diagProviders)
            {
                try
                {
                    // Providers should already be initialized/started by loader; still probe APIs
                    _ = await p.CollectDataAsync(cts.Token).ConfigureAwait(false);
                    var health = await p.CheckHealthAsync(cts.Token).ConfigureAwait(false);
                    logger.LogInformation("Provider {Name} health: {Status}", p.Name, health.Health);
                    await p.LogEventAsync(new DiagnosticEvent
                    {
                        Timestamp = DateTime.UtcNow,
                        Level = DiagnosticLevel.Information,
                        Category = "RuntimeVerify",
                        Message = "ProbeEvent",
                        Source = "VerifyPluginsCommand"
                    }, cts.Token).ConfigureAwait(false);
                    _ = await p.ExportDataAsync(DiagnosticExportFormat.Json, cts.Token).ConfigureAwait(false);

                    // Attribute this probe to a plugin via assembly name mapping
                    var asmName = Path.GetFileName(p.GetType().Assembly.Location);
                    if (!string.IsNullOrEmpty(asmName) && assemblyToPlugin.TryGetValue(asmName, out var pluginName))
                    {
                        if (!contractProbes.TryGetValue(pluginName, out var probes))
                        {
                            probes = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                            contractProbes[pluginName] = probes;
                        }
                        probes["IDiagnosticProvider"] = new { Probed = true, Ok = true };
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Diagnostic provider probe failed: {Name}", p.Name);
                    var asmName = Path.GetFileName(p.GetType().Assembly.Location);
                    if (!string.IsNullOrEmpty(asmName) && assemblyToPlugin.TryGetValue(asmName, out var pluginName))
                    {
                        if (!contractProbes.TryGetValue(pluginName, out var probes))
                        {
                            probes = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                            contractProbes[pluginName] = probes;
                        }
                        probes["IDiagnosticProvider"] = new { Probed = true, Ok = false, Message = ex.Message };
                    }
                }
            }

            // Build snapshot from PluginAdminService + metrics
            var status = await admin.GetSystemStatusAsync().ConfigureAwait(false);
            var snapshot = new
            {
                plugins = status.Plugins.Select(p => new
                {
                    Name = p.Name,
                    Version = p.Version,
                    State = p.IsLoaded ? (string)(p.Health == PluginHealthStatus.Healthy ? "Running" : (p.Health == PluginHealthStatus.Degraded ? "Degraded" : "Running")) : (string)"Failed",
                    MemoryUsageMB = (long)Math.Max(0, (p.MemoryUsage ?? 0) / 1024 / 1024),
                    LoadDurationMs = p.LoadDuration?.TotalMilliseconds ?? 0,
                    HealthStatusReason = p.HealthMessage,
                    DegradedSince = (DateTime?)null,
                    ErrorMessage = p.LoadError,
                    StackTrace = (string?)null,
                    ContractProbes = contractProbes.TryGetValue(p.Name, out var probes) ? probes : null
                }),
                Timestamp = DateTime.UtcNow,
                Filters = new
                {
                    include = include,
                    exclude = exclude
                }
            };

            // Write JSON
            var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
            Directory.CreateDirectory(Path.GetDirectoryName(output.FullName)!);
            await File.WriteAllTextAsync(output.FullName, json, cts.Token).ConfigureAwait(false);

            System.Console.ForegroundColor = ConsoleColor.Green;
            System.Console.WriteLine($"✓ Wrote plugin health snapshot: {output.FullName}");
            System.Console.ResetColor();
        }, pathsOption, outputOption, timeoutOption, includeOption, excludeOption);

        return cmd;
    }
}
