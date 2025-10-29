using Arch.Core;
using LablabBean.Contracts.Game.Models;
using LablabBean.Contracts.Game.UI;
using LablabBean.Contracts.Game.UI.Services;
using LablabBean.Contracts.UI.Models;
using LablabBean.Contracts.UI.Services;
using LablabBean.Game.Core.Maps;
using LablabBean.Game.Core.Components;
using LablabBean.Game.Core.Systems;
using LablabBean.Game.TerminalUI.Services;
using LablabBean.Game.TerminalUI.Views;
using LablabBean.Rendering.Contracts;
using LablabBean.Game.TerminalUI.Styles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Terminal.Gui;
using ContractsPosition = LablabBean.Contracts.Game.Models.Position;
using CorePosition = LablabBean.Game.Core.Components.Position;

namespace LablabBean.Game.TerminalUI;

/// <summary>
/// Terminal.Gui adapter implementing IUiService and IDungeonCrawlerUI.
/// Phase 3: Full Terminal.Gui v2 API implementation with HUD, WorldView, and ActivityLog.
/// </summary>
public class TerminalUiAdapter : IService, IDungeonCrawlerUI
{
    private readonly ISceneRenderer _sceneRenderer;
    private readonly ILogger _logger;
    private readonly IConfiguration? _configuration;
    private readonly TerminalRenderStyles _styles;
    private Window? _mainWindow;
    private HudService? _hudService;
    private WorldViewService? _worldViewService;
    private ActivityLogView? _activityLogView;
    private IActivityLog? _activityLog;
    private World? _currentWorld;
    private DungeonMap? _currentMap;
    private bool _preferHighQuality;
    private Tileset? _tileset;
    private TileRasterizer? _rasterizer;
    private ILoggerFactory? _loggerFactory;

    public TerminalUiAdapter(ISceneRenderer sceneRenderer, ILogger logger, IConfiguration? configuration = null, ILoggerFactory? loggerFactory = null, TerminalRenderStyles? styles = null)
    {
        _sceneRenderer = sceneRenderer;
        _logger = logger;
        _configuration = configuration;
        _loggerFactory = loggerFactory;
        _styles = styles ?? TerminalRenderStyles.Default();
    }

    #region IService Implementation

    public Task InitializeAsync(UIInitOptions options, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing Terminal UI Adapter");

        // T059, T060: Check renderer capabilities and configuration
        string? preferHighQualityStr = _configuration?["Rendering:Terminal:PreferHighQuality"];
        _preferHighQuality = string.IsNullOrEmpty(preferHighQualityStr) || bool.Parse(preferHighQualityStr);
        bool supportsImageMode = _sceneRenderer.SupportsImageMode;

        // T061: Log image mode support
        _logger.LogInformation("Renderer supports image mode: {SupportsImageMode}, PreferHighQuality: {PreferHighQuality}",
            supportsImageMode, _preferHighQuality);

        // T065, T066: Initialize tileset and rasterizer if needed
        if (supportsImageMode && _preferHighQuality && _loggerFactory != null)
        {
            string? tilesetPath = _configuration?["Rendering:Terminal:Tileset"];
            string? tileSizeStr = _configuration?["Rendering:Terminal:TileSize"];
            int tileSize = string.IsNullOrEmpty(tileSizeStr) ? 16 : int.Parse(tileSizeStr);

            if (!string.IsNullOrEmpty(tilesetPath))
            {
                var loader = new TilesetLoader(_loggerFactory.CreateLogger<TilesetLoader>());
                _tileset = loader.Load(tilesetPath, tileSize);
                if (_tileset != null)
                {
                    _rasterizer = new TileRasterizer(_loggerFactory.CreateLogger<TileRasterizer>());
                    _logger.LogInformation("Tileset loaded for image mode rendering");
                }
            }
        }

        Initialize();
        return Task.CompletedTask;
    }

    public Task RenderViewportAsync(ViewportBounds viewport, IReadOnlyCollection<Contracts.Game.Models.EntitySnapshot> entities)
    {
        _logger.LogDebug("Render viewport: {EntityCount} entities", entities.Count);

        // If we have a world and map, render them
        if (_currentWorld != null && _currentMap != null && _worldViewService != null && _hudService != null)
        {
            _hudService.Update(_currentWorld);

            // T068: Choose rendering mode based on capabilities
            TileBuffer? tileBuffer = null;

            if (_sceneRenderer.SupportsImageMode && _preferHighQuality && _tileset != null && _rasterizer != null)
            {
                // Use image mode rendering
                tileBuffer = BuildImageTileBuffer(_currentWorld, _currentMap);
            }

            // Fallback to glyph mode
            if (tileBuffer == null)
            {
                tileBuffer = BuildGlyphBuffer(_currentWorld, _currentMap);
            }

            if (tileBuffer != null)
            {
                _ = _sceneRenderer.RenderAsync(tileBuffer, CancellationToken.None);
            }
        }

        return Task.CompletedTask;
    }

    public Task UpdateDisplayAsync()
    {
        _logger.LogDebug("Display update requested");
        return Task.CompletedTask;
    }

    public Task HandleInputAsync(InputCommand command)
    {
        _logger.LogDebug("Input command: {Command}", command);
        return Task.CompletedTask;
    }

    public ViewportBounds GetViewport()
    {
        return new ViewportBounds(new ContractsPosition(0, 0), 80, 24);
    }

    public void SetViewportCenter(ContractsPosition centerPosition)
    {
        _logger.LogDebug("Set viewport center: ({X}, {Y})", centerPosition.X, centerPosition.Y);
    }

    public void Initialize()
    {
        _mainWindow = new Window
        {
            Title = "LablabBean - Dungeon Crawler",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        // Initialize services (placeholder - need proper DI)
        // For now, create with temporary logger adapters
        var inventorySystem = new InventorySystem(new LoggerAdapter<InventorySystem>(_logger));
        _hudService = new HudService(new LoggerAdapter<HudService>(_logger), inventorySystem);
        _worldViewService = new WorldViewService(new LoggerAdapter<WorldViewService>(_logger));
        _activityLogView = new ActivityLogView("Activity Log");

        // Add views to main window
        _mainWindow.Add(_worldViewService.WorldView);
        _mainWindow.Add(_hudService.HudView);
        _mainWindow.Add(_activityLogView);

        _logger.LogInformation("Terminal UI adapter initialized with full HUD, WorldView, and ActivityLog");

        // Initialize scene renderer with a basic palette and set render target if supported
        var paletteList = _styles.Palette ?? TerminalRenderStyles.Default().Palette!;
        var defaultPalette = new Palette(paletteList);

        try
        {
            _sceneRenderer.InitializeAsync(defaultPalette, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch { /* best effort */ }
    }

    public Window GetMainWindow()
    {
        return _mainWindow ?? throw new InvalidOperationException("UI not initialized");
    }

    public View? GetWorldView() => _worldViewService?.WorldView;
    public View? GetWorldRenderView() => _worldViewService?.RenderViewControl;

    #endregion

    #region Rendering Helpers

    // T062: Private method to build image tile buffer
    private TileBuffer? BuildImageTileBuffer(World world, DungeonMap map)
    {
        if (_worldViewService == null || _tileset == null || _rasterizer == null) return null;

        try
        {
            if (!_worldViewService.TryBuildGlyphArray(world, map, out var glyphs)) return null;

            int height = glyphs.GetLength(0);
            int width = glyphs.GetLength(1);

            // T063: Allocate ImageTile array
            ImageTile[,] imageTiles = new ImageTile[height, width];

            // T064: Map game tiles to tile IDs
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    char glyph = glyphs[y, x];
                    int tileId = MapGlyphToTileId(glyph);
                    imageTiles[y, x] = new ImageTile(tileId, null, 255); // Base tile without tint, full opacity
                }
            }

            // T065: Apply entity colors as tint colors
            if (_worldViewService.TryComputeCamera(world, map, out var camX, out var camY))
            {
                var query = new QueryDescription().WithAll<CorePosition, Renderable, Visible>();
                world.Query(in query, (Entity e, ref CorePosition pos, ref Renderable renderable, ref Visible vis) =>
                {
                    if (!vis.IsVisible) return;
                    if (!map.IsInFOV(pos.Point)) return;
                    int vx = pos.Point.X - camX;
                    int vy = pos.Point.Y - camY;
                    if (vx < 0 || vy < 0 || vx >= width || vy >= height) return;

                    int tileId = MapRenderableToTileId(renderable);
                    uint tintColor = ToArgb(renderable.Foreground);
                    imageTiles[vy, vx] = new ImageTile(tileId, tintColor, 255); // Full opacity
                });
            }

            // T066, T067: Rasterize tiles and return TileBuffer
            byte[]? pixelData = _rasterizer.Rasterize(imageTiles, _tileset);

            if (pixelData != null)
            {
                var tileBuffer = TileBuffer.CreateImageBuffer(width * _tileset.TileSize, height * _tileset.TileSize);
                tileBuffer.PixelData = pixelData;
                return tileBuffer;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build image tile buffer, falling back to glyph mode");
        }

        return null;
    }

    private TileBuffer? BuildGlyphBuffer(World world, DungeonMap map)
    {
        if (_worldViewService == null) return null;

        try
        {
            if (!_worldViewService.TryBuildGlyphArray(world, map, out var glyphs)) return null;

            int height = glyphs.GetLength(0);
            int width = glyphs.GetLength(1);
            var tileBuffer = new TileBuffer(width, height, glyphMode: true);

            // Build per-cell entity color overrides within viewport (highest Z wins)
            uint[,] entFg = new uint[height, width];
            uint[,] entBg = new uint[height, width];
            int[,] entZ = new int[height, width];
            for (int yy = 0; yy < height; yy++) for (int xx = 0; xx < width; xx++) entZ[yy, xx] = int.MinValue;

            if (_worldViewService.TryComputeCamera(world, map, out var camX, out var camY))
            {
                var query = new QueryDescription().WithAll<CorePosition, Renderable, Visible>();
                world.Query(in query, (Entity e, ref CorePosition pos, ref Renderable renderable, ref Visible vis) =>
                {
                    if (!vis.IsVisible) return;
                    if (!map.IsInFOV(pos.Point)) return;
                    int vx = pos.Point.X - camX;
                    int vy = pos.Point.Y - camY;
                    if (vx < 0 || vy < 0 || vx >= width || vy >= height) return;
                    if (renderable.ZOrder <= entZ[vy, vx]) return;
                    entZ[vy, vx] = renderable.ZOrder;
                    entFg[vy, vx] = ToArgb(renderable.Foreground);
                    entBg[vy, vx] = ToArgb(renderable.Background);
                });
            }

            if (tileBuffer.Glyphs != null)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        var ch = glyphs[y, x];
                        ColorRef fg;
                        ColorRef bg;
                        if (entZ[y, x] != int.MinValue)
                        {
                            fg = new ColorRef(0, entFg[y, x]);
                            bg = new ColorRef(0, entBg[y, x]);
                        }
                        else
                        {
                            var style = _styles.LookupForGlyph(ch);
                            fg = new ColorRef(0, style.ForegroundArgb);
                            bg = new ColorRef(0, style.BackgroundArgb);
                        }
                        tileBuffer.Glyphs[y, x] = new Glyph(ch, fg, bg);
                    }
                }
            }

            return tileBuffer;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build glyph buffer");
            return null;
        }
    }

    // T064: Map glyphs to tile IDs
    private int MapGlyphToTileId(char glyph)
    {
        return glyph switch
        {
            '.' => 0,  // Floor
            '#' => 1,  // Wall
            '+' => 2,  // Door
            _ => 0     // Default to floor
        };
    }

    private int MapRenderableToTileId(Renderable renderable)
    {
        return renderable.Glyph switch
        {
            '@' => 10, // Player
            'g' => 11, // Goblin/Enemy
            'o' => 11, // Orc/Enemy
            'i' => 20, // Item
            _ => 0     // Default
        };
    }

    #endregion

    #region IDungeonCrawlerUI Implementation

    public void ToggleHud()
    {
        _logger.LogInformation("HUD toggle requested");
    }

    public void ShowDialogue(string speaker, string text, string[]? choices = null)
    {
        _logger.LogInformation("Dialogue: {Speaker} - {Text}", speaker, text);
    }

    public void HideDialogue()
    {
        _logger.LogDebug("Hide dialogue requested");
    }

    public void ShowQuests()
    {
        _logger.LogInformation("Show quests requested");
    }

    public void HideQuests()
    {
        _logger.LogDebug("Hide quests requested");
    }

    public void ShowInventory()
    {
        _logger.LogInformation("Show inventory requested");
    }

    public void HideInventory()
    {
        _logger.LogDebug("Hide inventory requested");
    }

    public void UpdatePlayerStats(int health, int maxHealth, int mana, int maxMana, int level, int experience)
    {
        _logger.LogDebug("Player stats updated: HP {Health}/{MaxHealth}, Mana {Mana}/{MaxMana}, Level {Level}, XP {Experience}",
            health, maxHealth, mana, maxMana, level, experience);

        // Update HUD if available
        if (_hudService != null && _currentWorld != null)
        {
            _hudService.Update(_currentWorld);
        }
    }

    public void SetCameraFollow(int entityId)
    {
        _logger.LogDebug("Camera follow entity: {EntityId}", entityId);
    }

    // Helper method to set current world and map for rendering
    public void SetWorldContext(World world, DungeonMap map)
    {
        _currentWorld = world;
        _currentMap = map;
    }

    public void BindActivityLog(IActivityLog activityLog)
    {
        _activityLog = activityLog ?? throw new ArgumentNullException(nameof(activityLog));
        _activityLogView?.Bind(_activityLog);
    }

    #endregion

    private static uint ToArgb(SadRogue.Primitives.Color c)
    {
        return (0xFFu << 24) | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
    }
}

/// <summary>
/// Helper class to adapt ILogger to ILogger<T>
/// </summary>
internal class LoggerAdapter<T> : ILogger<T>
{
    private readonly ILogger _logger;

    public LoggerAdapter(ILogger logger)
    {
        _logger = logger;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        => _logger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel)
        => _logger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => _logger.Log(logLevel, eventId, state, exception, formatter);
}
