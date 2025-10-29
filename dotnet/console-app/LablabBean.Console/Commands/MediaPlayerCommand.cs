using System.CommandLine;
using System.Reactive.Linq;
using LablabBean.Contracts.Media;
using LablabBean.Contracts.Media.DTOs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LablabBean.Console.Commands;

/// <summary>
/// CLI command for media playback
/// </summary>
public static class MediaPlayerCommand
{
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("play", "Play media files in the terminal");

        var fileArgument = new Argument<string>(
            name: "file",
            description: "Path to the media file (video/audio)");

        var volumeOption = new Option<double>(
            name: "--volume",
            description: "Initial volume (0.0 to 1.0)",
            getDefaultValue: () => 0.8);
        volumeOption.AddAlias("-v");

        var loopOption = new Option<bool>(
            name: "--loop",
            description: "Loop playback",
            getDefaultValue: () => false);
        loopOption.AddAlias("-l");

        var autoplayOption = new Option<bool>(
            name: "--autoplay",
            description: "Start playing automatically",
            getDefaultValue: () => true);
        autoplayOption.AddAlias("-a");

        command.AddArgument(fileArgument);
        command.AddOption(volumeOption);
        command.AddOption(loopOption);
        command.AddOption(autoplayOption);

        command.SetHandler(async (file, volume, loop, autoplay) =>
        {
            await PlayMediaAsync(serviceProvider, file, volume, loop, autoplay);
        }, fileArgument, volumeOption, loopOption, autoplayOption);

        return command;
    }

    private static async Task PlayMediaAsync(
        IServiceProvider serviceProvider,
        string filePath,
        double volume,
        bool loop,
        bool autoplay)
    {
        var mediaService = serviceProvider.GetRequiredService<IMediaService>();
        var logger = serviceProvider.GetRequiredService<ILogger<IMediaService>>();

        try
        {
            // Validate file exists
            if (!File.Exists(filePath))
            {
                System.Console.WriteLine($"❌ Error: File not found: {filePath}");
                return;
            }

            System.Console.WriteLine($"🎬 Loading media: {Path.GetFileName(filePath)}");

            // Load media
            await mediaService.LoadAsync(filePath);

            // Set initial volume
            if (Math.Abs(volume - 0.8) > 0.001)
            {
                await mediaService.SetVolumeAsync((float)volume);
            }

            // Subscribe to state changes
            var stateSubscription = mediaService.PlaybackState.Subscribe(state =>
            {
                System.Console.WriteLine($"📊 State: {state.Status}");
            });

            // Subscribe to position updates (show every second)
            var lastPosition = TimeSpan.Zero;
            var duration = TimeSpan.Zero;
            var positionSubscription = mediaService.Position.Subscribe(position =>
            {
                if ((position - lastPosition).TotalSeconds >= 1.0)
                {
                    System.Console.WriteLine($"⏱️  {FormatTime(position)} / {FormatTime(duration)}");
                    lastPosition = position;
                }
            });

            // Get duration
            var durationSubscription = mediaService.Duration.Subscribe(d => duration = d);

            // Display metadata
            if (mediaService.CurrentMedia != null)
            {
                var meta = mediaService.CurrentMedia;
                System.Console.WriteLine("\n📝 Media Info:");
                System.Console.WriteLine($"   Duration: {FormatTime(meta.Duration)}");
                System.Console.WriteLine($"   Format: {meta.Format}");
                if (meta.Video != null)
                {
                    System.Console.WriteLine($"   Video: {meta.Video.Codec} @ {meta.Video.Width}x{meta.Video.Height} ({meta.Video.FrameRate:F2} fps)");
                }
                if (meta.Audio != null)
                {
                    System.Console.WriteLine($"   Audio: {meta.Audio.Codec} ({meta.Audio.SampleRate} Hz, {meta.Audio.Channels} channels)");
                }
                if (mediaService.ActiveRenderer != null)
                {
                    System.Console.WriteLine($"   Renderer: {mediaService.ActiveRenderer.GetType().Name}");
                }
                System.Console.WriteLine();
            }

            // Start playback if autoplay
            if (autoplay)
            {
                System.Console.WriteLine("▶️  Starting playback...");
                System.Console.WriteLine("   Controls:");
                System.Console.WriteLine("   • [Space]   Pause/Resume");
                System.Console.WriteLine("   • [← →]     Seek ±10s");
                System.Console.WriteLine("   • [↑ ↓]     Volume ±10%");
                System.Console.WriteLine("   • [Esc]     Stop\n");
                await mediaService.PlayAsync();

                // Interactive keyboard handling
                var cts = new CancellationTokenSource();
                System.Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };

                var keyTask = Task.Run(async () =>
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        if (System.Console.KeyAvailable)
                        {
                            var key = System.Console.ReadKey(intercept: true);
                            await HandleKeyPress(key, mediaService);
                        }
                        await Task.Delay(50, cts.Token);
                    }
                }, cts.Token);

                try
                {
                    await Task.Delay(Timeout.Infinite, cts.Token);
                }
                catch (TaskCanceledException)
                {
                    System.Console.WriteLine("\n⏹️  Stopping playback...");
                    await mediaService.StopAsync(cts.Token);
                }

                await keyTask.ConfigureAwait(false);
            }
            else
            {
                System.Console.WriteLine("⏸️  Ready to play. Use --autoplay to start automatically");
            }

            // Cleanup
            stateSubscription.Dispose();
            positionSubscription.Dispose();
            durationSubscription.Dispose();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to play media: {File}", filePath);
            System.Console.WriteLine($"❌ Error: {ex.Message}");
        }
    }

    private static async Task HandleKeyPress(ConsoleKeyInfo key, IMediaService mediaService)
    {
        try
        {
            switch (key.Key)
            {
                case ConsoleKey.Spacebar:
                    // Toggle pause/resume
                    var currentState = await mediaService.PlaybackState.FirstAsync();
                    if (currentState.Status == PlaybackStatus.Playing)
                    {
                        await mediaService.PauseAsync();
                        System.Console.WriteLine("⏸️  Paused");
                    }
                    else if (currentState.Status == PlaybackStatus.Paused)
                    {
                        await mediaService.PlayAsync(); // PlayAsync resumes when paused
                        System.Console.WriteLine("▶️  Resumed");
                    }
                    break;

                case ConsoleKey.Escape:
                    // Stop playback
                    System.Console.WriteLine("\n⏹️  Stopping...");
                    await mediaService.StopAsync();
                    Environment.Exit(0);
                    break;

                case ConsoleKey.LeftArrow:
                    // Seek backward 10 seconds
                    var currentPos = await mediaService.Position.FirstAsync();
                    var newPos = currentPos - TimeSpan.FromSeconds(10);
                    if (newPos < TimeSpan.Zero) newPos = TimeSpan.Zero;
                    await mediaService.SeekAsync(newPos);
                    System.Console.WriteLine($"⏪ Seek: {FormatTime(newPos)}");
                    break;

                case ConsoleKey.RightArrow:
                    // Seek forward 10 seconds
                    var pos = await mediaService.Position.FirstAsync();
                    var duration = await mediaService.Duration.FirstAsync();
                    var fwdPos = pos + TimeSpan.FromSeconds(10);
                    if (fwdPos > duration) fwdPos = duration;
                    await mediaService.SeekAsync(fwdPos);
                    System.Console.WriteLine($"⏩ Seek: {FormatTime(fwdPos)}");
                    break;

                case ConsoleKey.UpArrow:
                    // Volume up 10%
                    var vol = await mediaService.Volume.FirstAsync();
                    var newVol = Math.Min(1.0f, vol + 0.1f);
                    await mediaService.SetVolumeAsync(newVol);
                    System.Console.WriteLine($"🔊 Volume: {newVol:P0}");
                    break;

                case ConsoleKey.DownArrow:
                    // Volume down 10%
                    var currentVol = await mediaService.Volume.FirstAsync();
                    var lowerVol = Math.Max(0.0f, currentVol - 0.1f);
                    await mediaService.SetVolumeAsync(lowerVol);
                    System.Console.WriteLine($"🔉 Volume: {lowerVol:P0}");
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"⚠️  Control error: {ex.Message}");
        }
    }

    private static string FormatTime(TimeSpan time)
    {
        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours:D2}:{time.Minutes:D2}:{time.Seconds:D2}"
            : $"{time.Minutes:D2}:{time.Seconds:D2}";
    }
}
