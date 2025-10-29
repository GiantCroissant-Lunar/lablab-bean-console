namespace LablabBean.Console.Models;

public enum MenuActionType
{
    NewFile,
    OpenFile,
    SaveFile,
    Exit,
    Copy,
    Cut,
    Paste,
    Refresh,
    About,
    BuildProject,
    RunTests,
    ViewLogs
}

public record MenuAction(
    MenuActionType Type,
    string Label,
    string Description,
    string? Shortcut = null);
