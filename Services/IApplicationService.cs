using QingFeng.Models;

namespace QingFeng.Services;

public interface IApplicationService
{
    Task<List<Application>> GetAllApplicationsAsync();
    Task<Application?> GetApplicationAsync(string appId);
    Task<Application> SaveApplicationAsync(Application application);
    Task<bool> DeleteApplicationAsync(string appId);
    Task<bool> TogglePinToDockAsync(string appId);
    Task InitializeDefaultApplicationsAsync();
}
