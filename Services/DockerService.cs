using Docker.DotNet;
using Docker.DotNet.Models;
using QingFeng.Models;

namespace QingFeng.Services;

public class DockerService : IDockerService
{
    private DockerClient? _client;
    private bool _isAvailable = false;

    public DockerService()
    {
        try
        {
            // Try to connect to Docker
            var dockerUrl = Environment.GetEnvironmentVariable("DOCKER_HOST") ?? "unix:///var/run/docker.sock";
            
            // Support both Unix socket and Windows named pipe
            if (dockerUrl.StartsWith("unix://"))
            {
                _client = new DockerClientConfiguration(new Uri(dockerUrl)).CreateClient();
            }
            else if (File.Exists("\\\\.\\pipe\\docker_engine"))
            {
                _client = new DockerClientConfiguration(new Uri("npipe://./pipe/docker_engine")).CreateClient();
            }
            else
            {
                _client = new DockerClientConfiguration(new Uri("unix:///var/run/docker.sock")).CreateClient();
            }
            
            _isAvailable = true;
        }
        catch
        {
            _isAvailable = false;
        }
    }

    public async Task<bool> IsDockerAvailableAsync()
    {
        if (!_isAvailable || _client == null)
            return false;

        try
        {
            await _client.System.PingAsync();
            return true;
        }
        catch
        {
            _isAvailable = false;
            return false;
        }
    }

    public async Task<List<DockerContainerInfo>> GetContainersAsync(bool showAll = true)
    {
        if (!_isAvailable || _client == null)
            return new List<DockerContainerInfo>();

        try
        {
            var containers = await _client.Containers.ListContainersAsync(new ContainersListParameters
            {
                All = showAll
            });

            return containers.Select(c => new DockerContainerInfo
            {
                Id = c.ID[..12], // Short ID
                Name = c.Names.FirstOrDefault()?.TrimStart('/') ?? "unknown",
                Image = c.Image,
                Status = c.Status,
                State = c.State,
                Created = c.Created,
                Ports = c.Ports.Select(p => $"{p.PublicPort}:{p.PrivatePort}/{p.Type}").ToList(),
                Labels = new Dictionary<string, string>(c.Labels ?? new Dictionary<string, string>())
            }).ToList();
        }
        catch
        {
            return new List<DockerContainerInfo>();
        }
    }

    public async Task<List<DockerImageInfo>> GetImagesAsync()
    {
        if (!_isAvailable || _client == null)
            return new List<DockerImageInfo>();

        try
        {
            var images = await _client.Images.ListImagesAsync(new ImagesListParameters
            {
                All = false
            });

            return images.Select(i => new DockerImageInfo
            {
                Id = i.ID[7..19], // Short ID (remove sha256: prefix)
                RepoTags = i.RepoTags?.ToList() ?? new List<string>(),
                Size = i.Size,
                Created = i.Created
            }).ToList();
        }
        catch
        {
            return new List<DockerImageInfo>();
        }
    }

    public async Task StartContainerAsync(string containerId)
    {
        if (!_isAvailable || _client == null)
            throw new InvalidOperationException("Docker is not available");

        await _client.Containers.StartContainerAsync(containerId, new ContainerStartParameters());
    }

    public async Task StopContainerAsync(string containerId)
    {
        if (!_isAvailable || _client == null)
            throw new InvalidOperationException("Docker is not available");

        await _client.Containers.StopContainerAsync(containerId, new ContainerStopParameters());
    }

    public async Task RestartContainerAsync(string containerId)
    {
        if (!_isAvailable || _client == null)
            throw new InvalidOperationException("Docker is not available");

        await _client.Containers.RestartContainerAsync(containerId, new ContainerRestartParameters());
    }

    public async Task RemoveContainerAsync(string containerId)
    {
        if (!_isAvailable || _client == null)
            throw new InvalidOperationException("Docker is not available");

        await _client.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters
        {
            Force = true
        });
    }

    public async Task<string> GetContainerLogsAsync(string containerId, int tailLines = 1000)
    {
        if (!_isAvailable || _client == null)
            throw new InvalidOperationException("Docker is not available");

        var logsParameters = new ContainerLogsParameters
        {
            ShowStdout = true,
            ShowStderr = true,
            Tail = tailLines.ToString()
        };

        using var multiplexedStream = await _client.Containers.GetContainerLogsAsync(containerId, false, logsParameters, CancellationToken.None);
        using var memoryStream = new MemoryStream();
        await multiplexedStream.CopyOutputToAsync(memoryStream, memoryStream, Stream.Null, CancellationToken.None);
        
        memoryStream.Position = 0;
        using var reader = new StreamReader(memoryStream);
        return await reader.ReadToEndAsync();
    }
}
