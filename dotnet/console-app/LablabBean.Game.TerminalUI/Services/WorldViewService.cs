using System.Text;
using Arch.Core;
using Arch.Core.Extensions;
using LablabBean.Game.Core.Components;
using LablabBean.Game.Core.Maps;
using LablabBean.Game.TerminalUI.Views;
using Microsoft.Extensions.Logging;
using Terminal.Gui;
using TGuiColor = Terminal.Gui.Color;
using TGuiAttribute = Terminal.Gui.Attribute;
using SadColor = SadRogue.Primitives.Color;
using SadPoint = SadRogue.Primitives.Point;

namespace LablabBean.Game.TerminalUI.Services;

/// <summary>
/// Service for rendering the game world/scene in Terminal.Gui
/// Displays the dungeon map, entities, and handles the game viewport
/// </summary>
public class WorldViewService
{
    private readonly ILogger<WorldViewService> _logger;
    private readonly FrameView _worldFrame;
    private readonly MapView _renderView;
    private int _viewWidth;
    private int _viewHeight;

    public View WorldView => _worldFrame;
    public View RenderViewControl => _renderView;

    public WorldViewService(ILogger<WorldViewService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Create the main world frame (left side, leave space for debug log at bottom)
        _worldFrame = new FrameView()
        {
            Title = "Dungeon",
            X = 0,
            Y = 0,
            Width = Dim.Fill(30), // Leave space for HUD
            Height = Dim.Fill(10) // Leave space for debug log
        };

        // Create a custom MapView for rendering
        _renderView = new MapView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        _worldFrame.Add(_renderView);
    }

    /// <summary>
    /// Renders the game world
    /// </summary>
    public void Render(World world, DungeonMap map)
    {
        // Update dimensions from current view frame
        _viewWidth = _renderView.Frame.Width;
        _viewHeight = _renderView.Frame.Height;

        if (!TryBuildGlyphArray(world, map, out var buffer))
        {
            return;
        }

        _renderView.UpdateBuffer(buffer);
    }

    /// <summary>
    /// Builds a glyph array for the current viewport; returns false if dimensions/position invalid.
    /// </summary>
    public bool TryBuildGlyphArray(World world, DungeonMap map, out char[,] buffer)
    {
        buffer = default!;
        // Ensure dimensions are up to date from current render view
        _viewWidth = _renderView.Frame.Width;
        _viewHeight = _renderView.Frame.Height;

        _logger.LogInformation("Render called: viewWidth={Width}, viewHeight={Height}", _viewWidth, _viewHeight);

        if (_viewWidth <= 0 || _viewHeight <= 0)
        {
            _logger.LogWarning("Render aborted: view dimensions invalid");
            return false;
        }

        // Get player position for camera centering
        var playerPos = GetPlayerPosition(world);
        if (playerPos == null)
        {
            _logger.LogWarning("Render aborted: no player position");
            return false;
        }

        _logger.LogInformation("Rendering with player at {X},{Y}", playerPos.Value.X, playerPos.Value.Y);

        // Calculate camera offset to center on player
        int cameraX = playerPos.Value.X - _viewWidth / 2;
        int cameraY = playerPos.Value.Y - _viewHeight / 2;

        // Clamp camera to map bounds
        cameraX = Math.Max(0, Math.Min(cameraX, map.Width - _viewWidth));
        cameraY = Math.Max(0, Math.Min(cameraY, map.Height - _viewHeight));

        // Build the buffer
        buffer = new char[_viewHeight, _viewWidth];

        for (int y = 0; y < _viewHeight && y + cameraY < map.Height; y++)
        {
            for (int x = 0; x < _viewWidth && x + cameraX < map.Width; x++)
            {
                var worldPos = new SadPoint(x + cameraX, y + cameraY);
                char glyph = ' ';

                // Check if position is in FOV (currently visible)
                if (map.IsInFOV(worldPos))
                {
                    // Check for entities first (only visible in FOV)
                    var entityGlyph = GetEntityGlyphAt(world, map, worldPos);
                    if (entityGlyph.HasValue)
                    {
                        glyph = entityGlyph.Value;
                    }
                    else if (map.IsWalkable(worldPos))
                    {
                        glyph = '.';  // Floor - bright
                    }
                    else
                    {
                        glyph = '#';  // Wall - bright
                    }
                }
                else if (map.FogOfWar.IsExplored(worldPos))
                {
                    // Explored but not currently visible - use dimmer symbols
                    // No entities shown in fog of war
                    if (map.IsWalkable(worldPos))
                    {
                        glyph = '·';  // Dimmer floor (middle dot)
                    }
                    else
                    {
                        glyph = '▒';  // Dimmer wall (medium shade)
                    }
                }

                buffer[y, x] = glyph;
            }
        }

        _logger.LogInformation("Buffer created: {Width}x{Height}", _viewWidth, _viewHeight);
        return true;
    }

    /// <summary>
    /// Compute camera top-left based on player-centric viewport.
    /// Returns false if dimensions or player position are invalid.
    /// </summary>
    public bool TryComputeCamera(World world, DungeonMap map, out int cameraX, out int cameraY)
    {
        _viewWidth = _renderView.Frame.Width;
        _viewHeight = _renderView.Frame.Height;
        cameraX = 0; cameraY = 0;

        if (_viewWidth <= 0 || _viewHeight <= 0)
        {
            return false;
        }

        var playerPos = GetPlayerPosition(world);
        if (playerPos == null)
        {
            return false;
        }

        cameraX = playerPos.Value.X - _viewWidth / 2;
        cameraY = playerPos.Value.Y - _viewHeight / 2;
        cameraX = Math.Max(0, Math.Min(cameraX, map.Width - _viewWidth));
        cameraY = Math.Max(0, Math.Min(cameraY, map.Height - _viewHeight));
        return true;
    }

    /// <summary>
    /// Get current viewport dimensions from the render view.
    /// </summary>
    public (int width, int height) GetViewportSize()
    {
        return (_renderView.Frame.Width, _renderView.Frame.Height);
    }

    /// <summary>
    /// Gets entity glyph at position if any
    /// </summary>
    private char? GetEntityGlyphAt(World world, DungeonMap map, SadPoint worldPos)
    {
        var query = new QueryDescription().WithAll<Position, Renderable, Visible>();
        char? result = null;

        world.Query(in query, (Entity entity, ref Position pos, ref Renderable renderable, ref Visible visible) =>
        {
            if (visible.IsVisible && pos.Point == worldPos && map.IsInFOV(worldPos))
            {
                result = renderable.Glyph;
            }
        });

        return result;
    }

    /// <summary>
    /// Converts SadRogue color to Terminal.Gui color
    /// </summary>
    private TGuiColor ConvertColor(SadColor color)
    {
        // Map to closest Terminal.Gui color
        // This is a simplified mapping - you might want to enhance this

        if (color == SadColor.White)
            return TGuiColor.White;
        if (color == SadColor.Black)
            return TGuiColor.Black;
        if (color == SadColor.Red)
            return TGuiColor.Red;
        if (color == SadColor.Green)
            return TGuiColor.Green;
        if (color == SadColor.Blue)
            return TGuiColor.Blue;
        if (color == SadColor.Yellow)
            return TGuiColor.BrightYellow;
        if (color == SadColor.Cyan)
            return TGuiColor.Cyan;
        if (color == SadColor.Magenta)
            return TGuiColor.Magenta;
        if (color == SadColor.Gray)
            return TGuiColor.Gray;
        if (color == SadColor.DarkGray)
            return TGuiColor.DarkGray;
        if (color == SadColor.Brown)
            return TGuiColor.DarkGray;  // Brown not available in Terminal.Gui v2

        // Default to gray
        return TGuiColor.Gray;
    }

    /// <summary>
    /// Gets the player's position
    /// </summary>
    private SadPoint? GetPlayerPosition(World world)
    {
        var query = new QueryDescription().WithAll<Player, Position>();

        SadPoint? result = null;

        world.Query(in query, (Entity entity, ref Player player, ref Position pos) =>
        {
            result = pos.Point;
        });

        return result;
    }
}
