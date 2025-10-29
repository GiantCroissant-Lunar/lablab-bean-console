using System.CommandLine;
using LablabBean.Reporting.Contracts.Contracts;
using LablabBean.Reporting.Contracts.Models;
using LablabBean.Reporting.Providers.Build;
using LablabBean.Reporting.Analytics;
using Microsoft.Extensions.DependencyInjection;

namespace LablabBean.Console.Commands;

public static class ReportCommand
{
    public static Command Create(IServiceProvider serviceProvider)
    {
        var reportCommand = new Command("report", "Generate reports from build/session/plugin data");

        reportCommand.AddCommand(CreateBuildCommand(serviceProvider));
        reportCommand.AddCommand(CreateSessionCommand(serviceProvider));
        reportCommand.AddCommand(CreatePluginCommand(serviceProvider));

        return reportCommand;
    }

    private static Command CreateBuildCommand(IServiceProvider serviceProvider)
    {
        var command = new Command("build", "Generate build metrics report");

        var formatOption = new Option<string>(
            aliases: new[] { "--format", "-f" },
            getDefaultValue: () => "html",
            description: "Output format (html, csv)");

        var outputOption = new Option<FileInfo>(
            aliases: new[] { "--output", "-o" },
            description: "Output file path");
        outputOption.IsRequired = true;

        var dataOption = new Option<DirectoryInfo?>(
            aliases: new[] { "--data", "-d" },
            description: "Data directory (test results, coverage files)");

        command.AddOption(formatOption);
        command.AddOption(outputOption);
        command.AddOption(dataOption);

        command.SetHandler(async (format, output, data) =>
        {
            await HandleBuildReportAsync(serviceProvider, format, output, data);
        }, formatOption, outputOption, dataOption);

        return command;
    }

    private static Command CreateSessionCommand(IServiceProvider serviceProvider)
    {
        var command = new Command("session", "Generate game session statistics report");

        var formatOption = new Option<string>(
            aliases: new[] { "--format", "-f" },
            getDefaultValue: () => "html",
            description: "Output format (html, csv)");

        var outputOption = new Option<FileInfo>(
            aliases: new[] { "--output", "-o" },
            description: "Output file path");
        outputOption.IsRequired = true;

        var dataOption = new Option<FileInfo?>(
            aliases: new[] { "--data", "-d" },
            description: "Session data file (JSON)");

        command.AddOption(formatOption);
        command.AddOption(outputOption);
        command.AddOption(dataOption);

        command.SetHandler(async (format, output, data) =>
        {
            await HandleSessionReportAsync(serviceProvider, format, output, data);
        }, formatOption, outputOption, dataOption);

        return command;
    }

    private static Command CreatePluginCommand(IServiceProvider serviceProvider)
    {
        var command = new Command("plugin", "Generate plugin health report");

        var formatOption = new Option<string>(
            aliases: new[] { "--format", "-f" },
            getDefaultValue: () => "html",
            description: "Output format (html, csv)");

        var outputOption = new Option<FileInfo>(
            aliases: new[] { "--output", "-o" },
            description: "Output file path");
        outputOption.IsRequired = true;

        var dataOption = new Option<FileInfo?>(
            aliases: new[] { "--data", "-d" },
            description: "Plugin metrics file (JSON)");

        command.AddOption(formatOption);
        command.AddOption(outputOption);
        command.AddOption(dataOption);

        command.SetHandler(async (format, output, data) =>
        {
            await HandlePluginReportAsync(serviceProvider, format, output, data);
        }, formatOption, outputOption, dataOption);

        return command;
    }

    private static async Task<int> HandleBuildReportAsync(
        IServiceProvider serviceProvider,
        string format,
        FileInfo output,
        DirectoryInfo? dataDir)
    {
        try
        {
            System.Console.WriteLine($"Generating build metrics report ({format})...");

            // Get provider from DI (automatically registered by source generator)
            var provider = serviceProvider.GetService<BuildMetricsProvider>();

            if (provider == null)
            {
                throw new InvalidOperationException("BuildMetricsProvider not found. Ensure it's registered via AddReportProviders()");
            }

            var request = new ReportRequest
            {
                Format = format.ToLowerInvariant() == "html" ? ReportFormat.HTML : ReportFormat.CSV,
                OutputPath = output.FullName,
                DataPath = dataDir?.FullName
            };

            var data = await provider.GetReportDataAsync(request);

            if (data is not BuildMetricsData buildData)
            {
                throw new InvalidOperationException("Provider returned unexpected data type");
            }

            // Get renderer from DI
            var renderers = serviceProvider.GetServices<IReportRenderer>().ToList();
            var renderer = format.ToLowerInvariant() switch
            {
                "html" => renderers.FirstOrDefault(r => r.GetType().Name.Contains("Html")),
                "csv" => renderers.FirstOrDefault(r => r.GetType().Name.Contains("Csv")),
                _ => throw new ArgumentException($"Unsupported format: {format}")
            };

            if (renderer == null)
            {
                throw new InvalidOperationException($"Renderer for format '{format}' not found");
            }

            var result = await renderer.RenderAsync(request, buildData, CancellationToken.None);

            if (result.IsSuccess)
            {
                System.Console.ForegroundColor = ConsoleColor.Green;
                System.Console.WriteLine($"✅ Report generated: {result.OutputPath}");
                System.Console.ResetColor();
                System.Console.WriteLine($"   Tests: {buildData.TotalTests} ({buildData.PassedTests} passed, {buildData.FailedTests} failed)");
                System.Console.WriteLine($"   Coverage: {buildData.LineCoveragePercentage:F1}% line, {buildData.BranchCoveragePercentage:F1}% branch");
                if (result.FileSizeBytes > 0)
                {
                    System.Console.WriteLine($"   File size: {result.FileSizeBytes:N0} bytes");
                }
                return 0;
            }
            else
            {
                System.Console.ForegroundColor = ConsoleColor.Red;
                System.Console.WriteLine($"❌ Report generation failed:");
                foreach (var error in result.Errors)
                {
                    System.Console.WriteLine($"   {error}");
                }
                System.Console.ResetColor();
                return 1;
            }
        }
        catch (Exception ex)
        {
            System.Console.ForegroundColor = ConsoleColor.Red;
            System.Console.WriteLine($"❌ Error: {ex.Message}");
            System.Console.ResetColor();
            return 1;
        }
    }

    private static async Task<int> HandleSessionReportAsync(
        IServiceProvider serviceProvider,
        string format,
        FileInfo output,
        FileInfo? dataFile)
    {
        try
        {
            System.Console.WriteLine($"Generating session statistics report ({format})...");

            // Get provider from DI
            var provider = serviceProvider.GetService<SessionStatisticsProvider>();

            if (provider == null)
            {
                throw new InvalidOperationException("SessionStatisticsProvider not found. Ensure it's registered via AddReportProviders()");
            }

            var request = new ReportRequest
            {
                Format = format.ToLowerInvariant() == "html" ? ReportFormat.HTML : ReportFormat.CSV,
                OutputPath = output.FullName,
                DataPath = dataFile?.FullName
            };

            var data = await provider.GetReportDataAsync(request);

            if (data is not SessionStatisticsData sessionData)
            {
                throw new InvalidOperationException("Provider returned unexpected data type");
            }

            // Get renderer from DI
            var renderers = serviceProvider.GetServices<IReportRenderer>().ToList();
            var renderer = format.ToLowerInvariant() switch
            {
                "html" => renderers.FirstOrDefault(r => r.GetType().Name.Contains("Html")),
                "csv" => renderers.FirstOrDefault(r => r.GetType().Name.Contains("Csv")),
                _ => throw new ArgumentException($"Unsupported format: {format}")
            };

            if (renderer == null)
            {
                throw new InvalidOperationException($"Renderer for format '{format}' not found");
            }

            var result = await renderer.RenderAsync(request, sessionData, CancellationToken.None);

            if (result.IsSuccess)
            {
                System.Console.ForegroundColor = ConsoleColor.Green;
                System.Console.WriteLine($"✅ Report generated: {result.OutputPath}");
                System.Console.ResetColor();
                System.Console.WriteLine($"   Session: {sessionData.SessionId}");
                System.Console.WriteLine($"   Playtime: {sessionData.TotalPlaytime}");
                System.Console.WriteLine($"   K/D Ratio: {sessionData.KillDeathRatio:F2} ({sessionData.TotalKills} kills, {sessionData.TotalDeaths} deaths)");
                System.Console.WriteLine($"   Levels: {sessionData.LevelsCompleted}");
                if (result.FileSizeBytes > 0)
                {
                    System.Console.WriteLine($"   File size: {result.FileSizeBytes:N0} bytes");
                }
                return 0;
            }
            else
            {
                System.Console.ForegroundColor = ConsoleColor.Red;
                System.Console.WriteLine($"❌ Report generation failed:");
                foreach (var error in result.Errors)
                {
                    System.Console.WriteLine($"   {error}");
                }
                System.Console.ResetColor();
                return 1;
            }
        }
        catch (Exception ex)
        {
            System.Console.ForegroundColor = ConsoleColor.Red;
            System.Console.WriteLine($"❌ Error: {ex.Message}");
            System.Console.ResetColor();
            return 1;
        }
    }

    private static async Task<int> HandlePluginReportAsync(
        IServiceProvider serviceProvider,
        string format,
        FileInfo output,
        FileInfo? dataFile)
    {
        try
        {
            System.Console.WriteLine($"Generating plugin health report ({format})...");

            // Get provider from DI
            var provider = serviceProvider.GetService<PluginHealthProvider>();

            if (provider == null)
            {
                throw new InvalidOperationException("PluginHealthProvider not found. Ensure it's registered via AddReportProviders()");
            }

            var request = new ReportRequest
            {
                Format = format.ToLowerInvariant() == "html" ? ReportFormat.HTML : ReportFormat.CSV,
                OutputPath = output.FullName,
                DataPath = dataFile?.FullName
            };

            var data = await provider.GetReportDataAsync(request);

            if (data is not PluginHealthData pluginData)
            {
                throw new InvalidOperationException("Provider returned unexpected data type");
            }

            // Get renderer from DI
            var renderers = serviceProvider.GetServices<IReportRenderer>().ToList();
            var renderer = format.ToLowerInvariant() switch
            {
                "html" => renderers.FirstOrDefault(r => r.GetType().Name.Contains("Html")),
                "csv" => renderers.FirstOrDefault(r => r.GetType().Name.Contains("Csv")),
                _ => throw new ArgumentException($"Unsupported format: {format}")
            };

            if (renderer == null)
            {
                throw new InvalidOperationException($"Renderer for format '{format}' not found");
            }

            var result = await renderer.RenderAsync(request, pluginData, CancellationToken.None);

            if (result.IsSuccess)
            {
                System.Console.ForegroundColor = ConsoleColor.Green;
                System.Console.WriteLine($"✅ Report generated: {result.OutputPath}");
                System.Console.ResetColor();
                System.Console.WriteLine($"   Plugins: {pluginData.TotalPlugins} total ({pluginData.RunningPlugins} running, {pluginData.FailedPlugins} failed)");
                System.Console.WriteLine($"   Success Rate: {pluginData.SuccessRate:F1}%");
                System.Console.WriteLine($"   Memory: {pluginData.TotalMemoryUsageMB} MB");
                if (result.FileSizeBytes > 0)
                {
                    System.Console.WriteLine($"   File size: {result.FileSizeBytes:N0} bytes");
                }
                return 0;
            }
            else
            {
                System.Console.ForegroundColor = ConsoleColor.Red;
                System.Console.WriteLine($"❌ Report generation failed:");
                foreach (var error in result.Errors)
                {
                    System.Console.WriteLine($"   {error}");
                }
                System.Console.ResetColor();
                return 1;
            }
        }
        catch (Exception ex)
        {
            System.Console.ForegroundColor = ConsoleColor.Red;
            System.Console.WriteLine($"❌ Error: {ex.Message}");
            System.Console.ResetColor();
            return 1;
        }
    }
}
