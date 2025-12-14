namespace QingFeng.Models;

public class DockerContainerInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public DateTime Created { get; set; }
    public List<string> Ports { get; set; } = new();
    public Dictionary<string, string> Labels { get; set; } = new();
}

public class DockerImageInfo
{
    public string Id { get; set; } = string.Empty;
    public List<string> RepoTags { get; set; } = new();
    public long Size { get; set; }
    public DateTime Created { get; set; }
    
    public string SizeMB => $"{Size / 1024.0 / 1024.0:F2} MB";
}
