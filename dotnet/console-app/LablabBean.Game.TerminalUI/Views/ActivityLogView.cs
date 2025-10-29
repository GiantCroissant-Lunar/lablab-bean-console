using LablabBean.Contracts.Game.UI.Models;
using LablabBean.Contracts.Game.UI.Services;
using Terminal.Gui;

namespace LablabBean.Game.TerminalUI.Views;

/// <summary>
/// Terminal.Gui view that renders the ECS ActivityLog.
/// </summary>
public class ActivityLogView : FrameView
{
    private readonly ListView _listView;
    private long _lastSequence = -1;
    private int _maxLines = 100;
    private bool _showTimestamps = true;
    private IActivityLog? _service;

    public ActivityLogView(string title = "Activity") : base()
    {
        Title = title;
        X = 0;
        Y = Pos.AnchorEnd(10);
        Width = Dim.Fill(30);
        Height = 10;
        CanFocus = false;

        _listView = new ListView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            AllowsMarking = false,
            CanFocus = false
        };

        Add(_listView);
    }

    public void SetMaxLines(int max) => _maxLines = Math.Max(10, max);
    public void ShowTimestamps(bool show) => _showTimestamps = show;

    public void Bind(IActivityLog service)
    {
        _service = service;
        _service.Changed += OnServiceChanged;
        RefreshFromService();
    }

    private void OnServiceChanged(long sequence)
    {
        RefreshFromService();
    }

    private void RefreshFromService()
    {
        if (_service == null) return;
        var entries = _service.GetRecentEntries(_maxLines);
        var lines = BuildLines(entries);
        _listView.SetSource(new System.Collections.ObjectModel.ObservableCollection<string>(lines));
        if (lines.Count > 0)
        {
            _listView.SelectedItem = lines.Count - 1;
        }
    }

    private List<string> BuildLines(System.Collections.Generic.IReadOnlyList<ActivityEntryDto> entries)
    {
        var count = entries.Count;
        var start = Math.Max(0, count - _maxLines);
        var lines = new List<string>(Math.Min(_maxLines, count));
        for (int i = start; i < count; i++)
        {
            var e = entries[i];
            var ts = _showTimestamps ? $"[{e.Timestamp:HH:mm}] " : string.Empty;
            lines.Add($"{ts}{e.Icon} {e.Message}");
        }
        return lines;
    }
}
