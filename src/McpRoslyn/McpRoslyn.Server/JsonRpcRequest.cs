using System.Text.Json;

namespace McpRoslyn.Server;

public class JsonRpcRequest
{
    public string JsonRpc { get; set; } = "2.0";
    public object? Id { get; set; }
    public string Method { get; set; } = "";
    public JsonElement? Params { get; set; }
}