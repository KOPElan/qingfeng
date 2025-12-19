using QingFeng.Models;

namespace QingFeng.Services;

public interface IDockItemService
{
    Task<List<DockItem>> GetAllDockItemsAsync();
    Task<DockItem?> GetDockItemAsync(string itemId);
    Task<DockItem> SaveDockItemAsync(DockItem item);
    Task<bool> DeleteDockItemAsync(string itemId);
    Task AddApplicationToDockAsync(Application application);
    Task RemoveDockItemByAppIdAsync(string appId);
    Task InitializeDefaultDockItemsAsync();
}
