namespace McpRoslyn.Server;

public class WorkspaceInfo
{
    public string Id { get; set; } = "";
    public string Path { get; set; } = "";
    public string Type { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime LoadedAt { get; set; }
    public int ProjectCount { get; set; }
}