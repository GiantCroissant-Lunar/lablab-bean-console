using System.CommandLine;
using System.Reactive.Linq;
using LablabBean.Contracts.Media;
using LablabBean.Contracts.Media.DTOs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LablabBean.Console.Commands;

/// <summary>
/// CLI command for playlist management and playback
/// </summary>
public static class PlaylistCommand
{
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("playlist", "Manage and play media playlists");

        // Subcommands
        command.AddCommand(CreatePlayCommand(serviceProvider));
        command.AddCommand(CreateCreateCommand(serviceProvider));
        command.AddCommand(CreateAddCommand(serviceProvider));
        command.AddCommand(CreateListCommand(serviceProvider));

        return command;
    }

    private static Command CreatePlayCommand(IServiceProvider serviceProvider)
    {
        var command = new Command("play", "Play a playlist");

        var filesArgument = new Argument<string[]>(
            name: "files",
            description: "Media files to play");

        var volumeOption = new Option<double>(
            name: "--volume",
            description: "Initial volume (0.0 to 1.0)",
            getDefaultValue: () => 0.8);
        volumeOption.AddAlias("-v");

        var shuffleOption = new Option<bool>(
            name: "--shuffle",
            description: "Shuffle playback order",
            getDefaultValue: () => false);
        shuffleOption.AddAlias("-s");

        var repeatOption = new Option<string>(
            name: "--repeat",
            description: "Repeat mode: off, single, all",
            getDefaultValue: () => "off");
        repeatOption.AddAlias("-r");

        command.AddArgument(filesArgument);
        command.AddOption(volumeOption);
        command.AddOption(shuffleOption);
        command.AddOption(repeatOption);

        command.SetHandler(async (files, volume, shuffle, repeat) =>
        {
            await PlayPlaylistAsync(serviceProvider, files, volume, shuffle, repeat);
        }, filesArgument, volumeOption, shuffleOption, repeatOption);

        return command;
    }

    private static Command CreateCreateCommand(IServiceProvider serviceProvider)
    {
        var command = new Command("create", "Create a new playlist");

        var nameArgument = new Argument<string>(
            name: "name",
            description: "Playlist name");

        var filesArgument = new Argument<string[]>(
            name: "files",
            description: "Media files to add");

        command.AddArgument(nameArgument);
        command.AddArgument(filesArgument);

        command.SetHandler((name, files) =>
        {
            CreatePlaylistFile(name, files);
            System.Console.WriteLine($"✅ Created playlist: {name}.m3u");
        }, nameArgument, filesArgument);

        return command;
    }

    private static Command CreateAddCommand(IServiceProvider serviceProvider)
    {
        var command = new Command("add", "Add files to existing playlist");

        var playlistArgument = new Argument<string>(
            name: "playlist",
            description: "Playlist file (.m3u)");

        var filesArgument = new Argument<string[]>(
            name: "files",
            description: "Media files to add");

        command.AddArgument(playlistArgument);
        command.AddArgument(filesArgument);

        command.SetHandler((playlist, files) =>
        {
            AddToPlaylist(playlist, files);
            System.Console.WriteLine($"✅ Added {files.Length} file(s) to {playlist}");
        }, playlistArgument, filesArgument);

        return command;
    }

    private static Command CreateListCommand(IServiceProvider serviceProvider)
    {
        var command = new Command("list", "List files in playlist");

        var playlistArgument = new Argument<string>(
            name: "playlist",
            description: "Playlist file (.m3u)");

        command.AddArgument(playlistArgument);

        command.SetHandler((playlist) =>
        {
            ListPlaylist(playlist);
        }, playlistArgument);

        return command;
    }

    private static async Task PlayPlaylistAsync(
        IServiceProvider serviceProvider,
        string[] files,
        double volume,
        bool shuffle,
        string repeatModeStr)
    {
        var mediaService = serviceProvider.GetRequiredService<IMediaService>();
        var logger = serviceProvider.GetRequiredService<ILogger<IMediaService>>();

        try
        {
            // Validate files
            var validFiles = files.Where(File.Exists).ToArray();
            if (validFiles.Length == 0)
            {
                System.Console.WriteLine("❌ Error: No valid media files found");
                return;
            }

            if (validFiles.Length < files.Length)
            {
                System.Console.WriteLine($"⚠️  Warning: {files.Length - validFiles.Length} file(s) not found");
            }

            // Parse repeat mode
            var repeatMode = repeatModeStr.ToLower() switch
            {
                "single" => RepeatMode.Single,
                "all" => RepeatMode.All,
                _ => RepeatMode.Off
            };

            System.Console.WriteLine($"🎵 Playing playlist: {validFiles.Length} file(s)");
            System.Console.WriteLine($"   Shuffle: {(shuffle ? "On" : "Off")}");
            System.Console.WriteLine($"   Repeat: {repeatMode}");
            System.Console.WriteLine();

            // Create playlist
            var playlist = new Playlist
            {
                Id = Guid.NewGuid().ToString(),
                Name = "CLI Playlist",
                Items = shuffle ? validFiles.OrderBy(_ => Guid.NewGuid()).ToList() : validFiles.ToList(),
                ShuffleEnabled = shuffle,
                RepeatMode = repeatMode,
                CurrentIndex = 0
            };

            // Play each file
            var currentIndex = 0;
            var cts = new CancellationTokenSource();

            System.Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            while (currentIndex < playlist.Items.Count && !cts.Token.IsCancellationRequested)
            {
                var file = playlist.Items[currentIndex];

                System.Console.WriteLine($"🎬 [{currentIndex + 1}/{playlist.Items.Count}] {Path.GetFileName(file)}");

                try
                {
                    await mediaService.LoadAsync(file, cts.Token);

                    if (Math.Abs(volume - 0.8) > 0.001)
                    {
                        await mediaService.SetVolumeAsync((float)volume, cts.Token);
                    }

                    // Display metadata
                    if (mediaService.CurrentMedia != null)
                    {
                        var meta = mediaService.CurrentMedia;
                        System.Console.WriteLine($"   Duration: {FormatTime(meta.Duration)}");
                        if (meta.Video != null)
                        {
                            System.Console.WriteLine($"   Video: {meta.Video.Codec} @ {meta.Video.Width}x{meta.Video.Height}");
                        }
                        if (meta.Audio != null)
                        {
                            System.Console.WriteLine($"   Audio: {meta.Audio.Codec}");
                        }
                    }

                    System.Console.WriteLine("   Controls: [Space] Pause | [N] Next | [P] Previous | [Esc] Stop\n");

                    await mediaService.PlayAsync(cts.Token);

                    // Keyboard control loop
                    var playbackComplete = false;
                    var shouldStop = false;
                    var shouldSkip = false;
                    var skipDirection = 0; // -1 = previous, 1 = next

                    var keyTask = Task.Run(async () =>
                    {
                        while (!cts.Token.IsCancellationRequested && !playbackComplete)
                        {
                            if (System.Console.KeyAvailable)
                            {
                                var key = System.Console.ReadKey(intercept: true);
                                var result = await HandlePlaylistKeyPress(key, mediaService, currentIndex, playlist);

                                if (result.Action == KeyAction.Stop)
                                {
                                    shouldStop = true;
                                    playbackComplete = true;
                                    cts.Cancel();
                                    return;
                                }
                                else if (result.Action == KeyAction.Next || result.Action == KeyAction.Previous)
                                {
                                    shouldSkip = true;
                                    skipDirection = result.Action == KeyAction.Next ? 1 : -1;
                                    await mediaService.StopAsync(cts.Token);
                                    playbackComplete = true;
                                    return;
                                }
                            }
                            await Task.Delay(50, cts.Token);
                        }
                    }, cts.Token);

                    // Wait for playback to complete or user action
                    var duration = await mediaService.Duration.FirstAsync();
                    var startTime = DateTime.UtcNow;

                    while (!playbackComplete && !cts.Token.IsCancellationRequested)
                    {
                        var elapsed = DateTime.UtcNow - startTime;
                        if (elapsed >= duration)
                        {
                            playbackComplete = true;
                            break;
                        }
                        await Task.Delay(100, cts.Token);
                    }

                    await keyTask.ConfigureAwait(false);

                    // Handle skip actions
                    if (shouldSkip)
                    {
                        if (skipDirection == 1 && currentIndex < playlist.Items.Count - 1)
                        {
                            currentIndex++;
                        }
                        else if (skipDirection == -1 && currentIndex > 0)
                        {
                            currentIndex--;
                        }
                        continue; // Skip the automatic increment below
                    }

                    if (shouldStop)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to play: {File}", file);
                    System.Console.WriteLine($"❌ Error: {ex.Message}\n");
                }

                // Move to next track (if not manually changed)
                if (!cts.Token.IsCancellationRequested)
                {
                    currentIndex++;

                    // Handle repeat mode
                    if (currentIndex >= playlist.Items.Count && repeatMode == RepeatMode.All)
                    {
                        currentIndex = 0;
                        System.Console.WriteLine("🔁 Repeating playlist...\n");
                    }
                }
            }

            System.Console.WriteLine("✅ Playlist complete");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to play playlist");
            System.Console.WriteLine($"❌ Error: {ex.Message}");
        }
    }

    private enum KeyAction
    {
        None,
        Next,
        Previous,
        Stop
    }

    private record KeyPressResult(KeyAction Action, int NewIndex = 0);

    private static async Task<KeyPressResult> HandlePlaylistKeyPress(
        ConsoleKeyInfo key,
        IMediaService mediaService,
        int currentIndex,
        Playlist playlist)
    {
        try
        {
            switch (key.Key)
            {
                case ConsoleKey.Spacebar:
                    var state = await mediaService.PlaybackState.FirstAsync();
                    if (state.Status == PlaybackStatus.Playing)
                    {
                        await mediaService.PauseAsync();
                        System.Console.WriteLine("⏸️  Paused");
                    }
                    else if (state.Status == PlaybackStatus.Paused)
                    {
                        await mediaService.PlayAsync();
                        System.Console.WriteLine("▶️  Resumed");
                    }
                    return new KeyPressResult(KeyAction.None);

                case ConsoleKey.N:
                    if (currentIndex < playlist.Items.Count - 1)
                    {
                        System.Console.WriteLine("⏭️  Next track");
                        return new KeyPressResult(KeyAction.Next, currentIndex + 1);
                    }
                    return new KeyPressResult(KeyAction.None);

                case ConsoleKey.P:
                    if (currentIndex > 0)
                    {
                        System.Console.WriteLine("⏮️  Previous track");
                        return new KeyPressResult(KeyAction.Previous, currentIndex - 1);
                    }
                    return new KeyPressResult(KeyAction.None);

                case ConsoleKey.Escape:
                    System.Console.WriteLine("\n⏹️  Stopping playlist...");
                    return new KeyPressResult(KeyAction.Stop);

                case ConsoleKey.LeftArrow:
                    var pos = await mediaService.Position.FirstAsync();
                    var newPos = pos - TimeSpan.FromSeconds(10);
                    if (newPos < TimeSpan.Zero) newPos = TimeSpan.Zero;
                    await mediaService.SeekAsync(newPos);
                    System.Console.WriteLine($"⏪ Seek: {FormatTime(newPos)}");
                    return new KeyPressResult(KeyAction.None);

                case ConsoleKey.RightArrow:
                    var currentPos = await mediaService.Position.FirstAsync();
                    var duration = await mediaService.Duration.FirstAsync();
                    var fwdPos = currentPos + TimeSpan.FromSeconds(10);
                    if (fwdPos > duration) fwdPos = duration;
                    await mediaService.SeekAsync(fwdPos);
                    System.Console.WriteLine($"⏩ Seek: {FormatTime(fwdPos)}");
                    return new KeyPressResult(KeyAction.None);

                case ConsoleKey.UpArrow:
                    var vol = await mediaService.Volume.FirstAsync();
                    var newVol = Math.Min(1.0f, vol + 0.1f);
                    await mediaService.SetVolumeAsync(newVol);
                    System.Console.WriteLine($"🔊 Volume: {newVol:P0}");
                    return new KeyPressResult(KeyAction.None);

                case ConsoleKey.DownArrow:
                    var currentVol = await mediaService.Volume.FirstAsync();
                    var lowerVol = Math.Max(0.0f, currentVol - 0.1f);
                    await mediaService.SetVolumeAsync(lowerVol);
                    System.Console.WriteLine($"🔉 Volume: {lowerVol:P0}");
                    return new KeyPressResult(KeyAction.None);
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"⚠️  Control error: {ex.Message}");
        }

        return new KeyPressResult(KeyAction.None);
    }

    private static void CreatePlaylistFile(string name, string[] files)
    {
        var filename = name.EndsWith(".m3u", StringComparison.OrdinalIgnoreCase) ? name : $"{name}.m3u";
        File.WriteAllLines(filename, files);
    }

    private static void AddToPlaylist(string playlist, string[] files)
    {
        var existing = File.Exists(playlist) ? File.ReadAllLines(playlist).ToList() : new List<string>();
        existing.AddRange(files);
        File.WriteAllLines(playlist, existing);
    }

    private static void ListPlaylist(string playlist)
    {
        if (!File.Exists(playlist))
        {
            System.Console.WriteLine($"❌ Error: Playlist not found: {playlist}");
            return;
        }

        var files = File.ReadAllLines(playlist);
        System.Console.WriteLine($"📋 Playlist: {Path.GetFileName(playlist)}");
        System.Console.WriteLine($"   Files: {files.Length}\n");

        for (int i = 0; i < files.Length; i++)
        {
            var exists = File.Exists(files[i]);
            var icon = exists ? "✅" : "❌";
            System.Console.WriteLine($"   {i + 1}. {icon} {Path.GetFileName(files[i])}");
        }
    }

    private static string FormatTime(TimeSpan time)
    {
        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours:D2}:{time.Minutes:D2}:{time.Seconds:D2}"
            : $"{time.Minutes:D2}:{time.Seconds:D2}";
    }
}
