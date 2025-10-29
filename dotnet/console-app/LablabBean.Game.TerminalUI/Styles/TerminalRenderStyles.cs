using System;
using System.Collections.Generic;

namespace LablabBean.Game.TerminalUI.Styles;

public sealed class TerminalRenderStyles
{
    public List<uint>? Palette { get; set; }
    public Style Floor { get; set; } = new Style('.', 0xFFC0C0C0, 0xFF000000);
    public Style Wall { get; set; } = new Style('#', 0xFF808080, 0xFF000000);
    public Style FloorExplored { get; set; } = new Style('·', 0xFF404040, 0xFF000000);
    public Style WallExplored { get; set; } = new Style('▒', 0xFF606060, 0xFF000000);
    public Style EntityDefault { get; set; } = new Style('@', 0xFFFFFFFF, 0xFF000000);

    public static TerminalRenderStyles Default()
    {
        return new TerminalRenderStyles
        {
            Palette = new List<uint>
            {
                0xFF000000, 0xFFFF0000, 0xFF00FF00, 0xFFFFFF00,
                0xFF0000FF, 0xFFFF00FF, 0xFF00FFFF, 0xFFB0B0B0,
                0xFF505050, 0xFFFF8080, 0xFF80FF80, 0xFFFFFF80,
                0xFF8080FF, 0xFFFF80FF, 0xFF80FFFF, 0xFFFFFFFF
            }
        };
    }

    public readonly record struct Style(char Glyph, uint ForegroundArgb, uint BackgroundArgb)
    {
        public char Glyph { get; init; } = Glyph;
        public uint ForegroundArgb { get; init; } = ForegroundArgb;
        public uint BackgroundArgb { get; init; } = BackgroundArgb;
    }

    public Style LookupForGlyph(char glyph)
    {
        return glyph switch
        {
            '.' => Floor,
            '#' => Wall,
            '·' => FloorExplored,
            '▒' => WallExplored,
            _ => EntityDefault
        };
    }
}
