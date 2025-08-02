using System.Text.Json;

namespace McpRoslyn.Server;

public class ToolCallParams
{
    public string Name { get; set; } = "";
    public JsonElement? Arguments { get; set; }
}