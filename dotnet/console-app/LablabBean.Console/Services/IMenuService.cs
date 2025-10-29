using LablabBean.Console.Models;

namespace LablabBean.Console.Services;

public interface IMenuService
{
    IEnumerable<MenuAction> GetFileMenuActions();
    IEnumerable<MenuAction> GetEditMenuActions();
    IEnumerable<MenuAction> GetViewMenuActions();
    IEnumerable<MenuAction> GetBuildMenuActions();
    void ExecuteAction(MenuActionType action);
}
