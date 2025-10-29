using LablabBean.Console.Models;
using Microsoft.Extensions.Logging;

namespace LablabBean.Console.Services;

public class MenuService : IMenuService
{
    private readonly ILogger<MenuService> _logger;

    public MenuService(ILogger<MenuService> logger)
    {
        _logger = logger;
    }

    public IEnumerable<MenuAction> GetFileMenuActions()
    {
        return new[]
        {
            new MenuAction(MenuActionType.NewFile, "New", "Create new file", "Ctrl+N"),
            new MenuAction(MenuActionType.OpenFile, "Open", "Open file", "Ctrl+O"),
            new MenuAction(MenuActionType.SaveFile, "Save", "Save file", "Ctrl+S"),
            new MenuAction(MenuActionType.Exit, "Exit", "Exit application", "Ctrl+Q")
        };
    }

    public IEnumerable<MenuAction> GetEditMenuActions()
    {
        return new[]
        {
            new MenuAction(MenuActionType.Copy, "Copy", "Copy selection", "Ctrl+C"),
            new MenuAction(MenuActionType.Cut, "Cut", "Cut selection", "Ctrl+X"),
            new MenuAction(MenuActionType.Paste, "Paste", "Paste from clipboard", "Ctrl+V")
        };
    }

    public IEnumerable<MenuAction> GetViewMenuActions()
    {
        return new[]
        {
            new MenuAction(MenuActionType.Refresh, "Refresh", "Refresh view", "F5"),
            new MenuAction(MenuActionType.ViewLogs, "View Logs", "View application logs", "Ctrl+L")
        };
    }

    public IEnumerable<MenuAction> GetBuildMenuActions()
    {
        return new[]
        {
            new MenuAction(MenuActionType.BuildProject, "Build", "Build project", "F6"),
            new MenuAction(MenuActionType.RunTests, "Run Tests", "Run all tests", "Ctrl+T")
        };
    }

    public void ExecuteAction(MenuActionType action)
    {
        _logger.LogInformation("Executing action: {Action}", action);

        // Action execution will be handled by the view
    }
}
