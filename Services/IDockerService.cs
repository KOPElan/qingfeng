using QingFeng.Models;

namespace QingFeng.Services;

public interface IDockerService
{
    Task<List<DockerContainerInfo>> GetContainersAsync(bool showAll = true);
    Task<List<DockerImageInfo>> GetImagesAsync();
    Task StartContainerAsync(string containerId);
    Task StopContainerAsync(string containerId);
    Task RestartContainerAsync(string containerId);
    Task RemoveContainerAsync(string containerId);
    Task<bool> IsDockerAvailableAsync();
}
