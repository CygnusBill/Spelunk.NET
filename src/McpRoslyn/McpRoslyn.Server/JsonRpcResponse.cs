namespace McpRoslyn.Server;

public class JsonRpcResponse
{
    public string JsonRpc { get; set; } = "2.0";
    public object? Id { get; set; }
    public object? Result { get; set; }
    public JsonRpcError? Error { get; set; }
}