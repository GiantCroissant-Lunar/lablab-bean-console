using System.CommandLine;
using LablabBean.Console.Commands;
using LablabBean.Console.Services;
using LablabBean.Game.Core.Services;
using LablabBean.Game.Core.Systems;
using LablabBean.Game.Core.Worlds;
using LablabBean.Infrastructure.Extensions;
using LablabBean.Plugins.Core;
using LablabBean.Reactive.Extensions;
using LablabBean.Reporting.Contracts.Contracts;
using LablabBean.Plugins.Reporting.Html;
using LablabBean.Plugins.Reporting.Csv;
using LablabBean.Reporting.Providers.Build;
using LablabBean.Reporting.Analytics;
using LablabBean.AI.Actors.Extensions;
using LablabBean.AI.Actors.Systems;
using LablabBean.AI.Agents.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

try
{
    // Check if CLI arguments are provided (report commands)
    if (args.Length > 0 && args[0] == "report")
    {
        // Build DI container for reporting
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Warning);
        });

        // Register report providers (will use source generator auto-registration when available)
        // For now, register manually until source generator is verified working
        services.AddTransient<BuildMetricsProvider>();
        services.AddTransient<SessionStatisticsProvider>();
        services.AddTransient<PluginHealthProvider>();

        // Register renderers
        services.AddSingleton<IReportRenderer, HtmlReportRenderer>();
        services.AddSingleton<IReportRenderer, CsvReportRenderer>();

        var serviceProvider = services.BuildServiceProvider();

        var rootCommand = new RootCommand("LablabBean Console - Dungeon Crawler Game & Reporting Tool");
        rootCommand.AddCommand(ReportCommand.Create(serviceProvider));

        return await rootCommand.InvokeAsync(args);
    }

    // Test IntelligentAISystem without TUI
    if (args.Length > 0 && args[0] == "test-ai")
    {
        try
        {
            System.Console.WriteLine("Starting IntelligentAISystem test...");
            var testHost = Host.CreateDefaultBuilder(args)
                .UseLablabBeanInfrastructure()
                .ConfigureServices((context, services) =>
                {
                    services.AddLablabBeanInfrastructure(context.Configuration);
                    services.AddAkkaActors(context.Configuration);
                    services.AddSemanticKernelAgents(context.Configuration);
                    services.AddSingleton<IntelligentAISystem>();
                    services.AddSingleton<IntelligentEntityFactory>();
                })
                .Build();

            System.Console.WriteLine("Host built successfully");
            var serviceProvider = testHost.Services;
            await LablabBean.Console.Tests.IntelligentAISystemTest.RunTest(serviceProvider);
            System.Console.WriteLine("Test completed successfully");
            return 0;
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Test failed: {ex.Message}");
            System.Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return 1;
        }
    }

    // Knowledge Base CLI
    if (args.Length > 0 && args[0] == "kb")
    {
        var kbHost = Host.CreateDefaultBuilder(args)
            .UseLablabBeanInfrastructure()
            .ConfigureServices((context, services) =>
            {
                services.AddLablabBeanInfrastructure(context.Configuration);
                services.AddSemanticKernelAgents(context.Configuration);
            })
            .Build();

        var serviceProvider = kbHost.Services;

        var rootCommand = new RootCommand("LablabBean Console - Knowledge Base Management");
        rootCommand.AddCommand(KnowledgeBaseCommand.Create(serviceProvider));

        return await rootCommand.InvokeAsync(args);
    }

    // Media Player CLI
    if (args.Length > 0 && (args[0] == "play" || args[0] == "playlist"))
    {
        var mediaHost = Host.CreateDefaultBuilder(args)
            .UseLablabBeanInfrastructure()
            .ConfigureServices((context, services) =>
            {
                services.AddLablabBeanInfrastructure(context.Configuration);
                services.AddLablabBeanReactive();

                // Note: Media player plugins are now loaded through the plugin system
                // They implement IPlugin interface and are discovered automatically
            })
            .Build();

        var serviceProvider = mediaHost.Services;

        var rootCommand = new RootCommand("LablabBean Console - Media Player");
        rootCommand.AddCommand(MediaPlayerCommand.Create(serviceProvider));
        rootCommand.AddCommand(PlaylistCommand.Create(serviceProvider));

        return await rootCommand.InvokeAsync(args);
    }

    // Plugins CLI (discovery / listing without starting TUI)
    if (args.Length > 0 && args[0] == "plugins")
    {
        var rootCommand = new RootCommand("LablabBean Console - Plugins CLI");
        var plugins = PluginsCommand.Create();
        plugins.AddCommand(VerifyPluginsCommand.Create());
        rootCommand.AddCommand(plugins);
        return await rootCommand.InvokeAsync(args);
    }

    // Otherwise, run interactive mode via UI plugin (no host-managed TUI)
    var host = Host.CreateDefaultBuilder(args)
        .UseLablabBeanInfrastructure()
        .ConfigureServices((context, services) =>
        {
            services.AddLablabBeanInfrastructure(context.Configuration);
            services.AddLablabBeanReactive();

            // Add plugin system
            services.AddPluginSystem(context.Configuration);

            // Note: Media player plugins are now loaded through the plugin system
            // They implement IPlugin interface and are discovered automatically

            // Add intelligent avatar system (Akka.NET + Semantic Kernel)
            services.AddAkkaActors(context.Configuration);
            services.AddSemanticKernelAgents(context.Configuration);

            // Add game framework services
            services.AddSingleton<GameWorldManager>();
            services.AddSingleton<MovementSystem>();
            services.AddSingleton<CombatSystem>();
            services.AddSingleton<AISystem>();
            services.AddSingleton<ActorSystem>();
            services.AddSingleton<InventorySystem>();
            services.AddSingleton<ItemSpawnSystem>();
            services.AddSingleton<StatusEffectSystem>();
            services.AddSingleton<GameStateManager>();

            // Optional intelligent AI services (used by gameplay plugins)
            services.AddSingleton<IntelligentAISystem>();
            services.AddSingleton<IntelligentEntityFactory>();
        })
        .Build();

    await host.RunAsync();
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}
