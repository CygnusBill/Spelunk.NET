using System.Text.Json;

namespace McpDotnet.Server;

public class ToolCallParams
{
    public string Name { get; set; } = "";
    public JsonElement? Arguments { get; set; }
}