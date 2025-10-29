using LablabBean.Game.Core.Components;
using LablabBean.Game.Core.Systems;
using Microsoft.Extensions.Logging;
using Terminal.Gui;
using Arch.Core;
using Arch.Core.Extensions;

namespace LablabBean.Game.TerminalUI.Services;

/// <summary>
/// Service for managing the HUD (Heads-Up Display) in Terminal.Gui
/// Displays player stats, inventory, messages, etc.
/// </summary>
public class HudService
{
    private readonly ILogger<HudService> _logger;
    private readonly FrameView _hudFrame;
    private readonly Label _levelLabel;
    private readonly Label _healthLabel;
    private readonly Label _statsLabel;
    private readonly FrameView _inventoryFrame;
    private readonly Label _inventoryLabel;
    private readonly InventorySystem _inventorySystem;

    public View HudView => _hudFrame;

    public HudService(ILogger<HudService> logger, InventorySystem inventorySystem)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _inventorySystem = inventorySystem ?? throw new ArgumentNullException(nameof(inventorySystem));

        // Create the main HUD frame (on the right side)
        _hudFrame = new FrameView()
        {
            Title = "HUD",
            X = Pos.AnchorEnd(30),
            Y = 0,
            Width = 30,
            Height = Dim.Fill(),
            CanFocus = false  // HUD should not steal focus from game
        };

        // Level display (at the top)
        _levelLabel = new Label
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill(2),
            Height = 2,
            Text = "Level: 1\nDepth: -30 ft"
        };

        // Health display
        _healthLabel = new Label
        {
            X = 1,
            Y = 3,
            Width = Dim.Fill(2),  // Leave margin for frame border
            Height = 3,
            Text = "Health: --/--"
        };

        // Stats display
        _statsLabel = new Label
        {
            X = 1,
            Y = 7,
            Width = Dim.Fill(2),  // Leave margin for frame border
            Height = 5,
            Text = "Stats:\n  ATK: --\n  DEF: --\n  SPD: --"
        };

        // Inventory display
        _inventoryFrame = new FrameView()
        {
            Title = "Inventory (0/20)",
            X = 1,
            Y = 13,
            Width = Dim.Fill(2),
            Height = Dim.Fill(2),
            CanFocus = false
        };

        _inventoryLabel = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Text = "  (Empty)"
        };

        _inventoryFrame.Add(_inventoryLabel);
        _hudFrame.Add(_levelLabel, _healthLabel, _statsLabel, _inventoryFrame);
    }

    /// <summary>
    /// Updates the HUD with current player information
    /// </summary>
    public void Update(World world)
    {
        var query = new QueryDescription().WithAll<Player, Health, Combat, Actor>();

        world.Query(in query, (Entity entity, ref Player player, ref Health health, ref Combat combat, ref Actor actor) =>
        {
            UpdatePlayerStats(player.Name, health, combat, actor);
            UpdateInventory(world, entity);
        });
    }

    /// <summary>
    /// Updates player stats display
    /// </summary>
    private void UpdatePlayerStats(string playerName, Health health, Combat combat, Actor actor)
    {
        // Update health
        _healthLabel.Text = $"Health: {health.Current}/{health.Maximum}\n" +
                           $"HP%: {health.Percentage:P0}\n" +
                           $"{GetHealthBar(health.Percentage)}";

        // Update stats
        _statsLabel.Text = $"Stats:\n" +
                          $"  ATK: {combat.Attack}\n" +
                          $"  DEF: {combat.Defense}\n" +
                          $"  SPD: {actor.Speed}\n" +
                          $"  NRG: {actor.Energy}";
    }

    /// <summary>
    /// Updates the level display
    /// </summary>
    public void UpdateLevelDisplay(int currentLevel, int personalBest, int depthInFeet)
    {
        _levelLabel.Text = $"Level: {currentLevel}\nDepth: -{depthInFeet} ft";

        if (currentLevel > personalBest)
        {
            _levelLabel.Text += " NEW!";
        }
    }

    /// <summary>
    /// Updates inventory display with current items
    /// </summary>
    public void UpdateInventory(World world, Entity playerEntity)
    {
        var items = _inventorySystem.GetInventoryItems(world, playerEntity);

        // Update inventory frame title with count
        var inventory = world.Has<Inventory>(playerEntity) ? world.Get<Inventory>(playerEntity) : default;
        var count = inventory.CurrentCount;
        var maxCapacity = inventory.MaxCapacity;
        var fullWarning = inventory.IsFull ? " (FULL)" : "";
        _inventoryFrame.Title = $"Inventory ({count}/{maxCapacity}){fullWarning}";

        // Build inventory display text
        if (items.Count == 0)
        {
            _inventoryLabel.Text = "  (Empty)";
            return;
        }

        var displayLines = new List<string>();
        foreach (var (itemEntity, item, itemCount, isEquipped) in items)
        {
            var countStr = itemCount > 1 ? $" ({itemCount})" : "";
            var equippedStr = isEquipped ? " [E]" : "";
            displayLines.Add($"  {item.Name}{countStr}{equippedStr}");
        }

        _inventoryLabel.Text = string.Join("\n", displayLines);
    }

    /// <summary>
    /// Creates a visual health bar
    /// </summary>
    private string GetHealthBar(float percentage)
    {
        int barLength = 20;
        int filled = (int)(barLength * percentage);
        int empty = barLength - filled;

        return "[" + new string('=', filled) + new string(' ', empty) + "]";
    }
}
