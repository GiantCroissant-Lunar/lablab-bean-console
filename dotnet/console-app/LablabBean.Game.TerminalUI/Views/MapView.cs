using System.Text;
using Terminal.Gui;

namespace LablabBean.Game.TerminalUI.Views;

/// <summary>
/// Custom view for rendering the dungeon map
/// </summary>
public class MapView : View
{
    private char[,]? _buffer;
    private int _bufferWidth;
    private int _bufferHeight;

    public MapView()
    {
        CanFocus = false;
    }

    /// <summary>
    /// Updates the buffer with new content
    /// </summary>
    public void UpdateBuffer(char[,] buffer)
    {
        _buffer = buffer;
        _bufferHeight = buffer.GetLength(0);
        _bufferWidth = buffer.GetLength(1);
        SetNeedsDraw();
    }

    /// <summary>
    /// Renders the buffer to screen
    /// </summary>
    public void RenderBuffer()
    {
        if (_buffer == null)
            return;

        // Ensure we don't draw outside view bounds
        int maxRows = Math.Min(_bufferHeight, Frame.Height);
        int maxCols = Math.Min(_bufferWidth, Frame.Width);

        // Draw character by character using AddRune
        for (int row = 0; row < maxRows; row++)
        {
            for (int col = 0; col < maxCols; col++)
            {
                char ch = _buffer[row, col];
                AddRune(col, row, new Rune(ch));
            }
        }
    }
}
