namespace McpDotnet.Server;

// Test interfaces
public interface IWorkspaceService
{
    Task<bool> LoadAsync(string path);
}

public interface IMessageLogger
{
    void Log(string message);
}

// Test classes
public class UserController
{
    private readonly IMessageLogger _logger;
    
    public string Name { get; set; } = "UserController";
    public int UserCount { get; private set; }
    public bool IsActive { get; set; } = true;
    
    public UserController(IMessageLogger logger)
    {
        _logger = logger;
    }
    
    public string GetUser(int id)
    {
        _logger.Log($"Getting user {id}");
        return $"User {id}";
    }
    
    public async Task<string> GetUserAsync(int id) 
    {
        _logger.Log($"Getting user {id} async");
        var user = await LoadUserFromDatabaseAsync(id);
        return ProcessUser(user);
    }
    
    private async Task<string> LoadUserFromDatabaseAsync(int id)
    {
        await Task.Delay(100); // Simulate DB access
        return $"User {id}";
    }
    
    private string ProcessUser(string user)
    {
        var defaultName = GetDefaultUserName();
        return user == "" ? defaultName : user;
    }
    
    public void DeleteUser(int id) 
    { 
        _logger.Log($"Deleting user {id}");
    }
    
    public static string GetDefaultUserName() => "Default";
}

public class ProductController
{
    public string ProductName { get; set; } = "";
    public decimal Price { get; set; }
    public bool IsAvailable { get; } = true;
    
    public string GetProduct(int id) => $"Product {id}";
    public async Task<string> GetProductAsync(int id) => await Task.FromResult($"Product {id}");
    public void UpdateProduct(int id, string name) { }
    public static decimal GetDefaultPrice() => 0.0m;
}

public abstract class BaseRepository<T>
{
    public abstract Task<T> GetByIdAsync(int id);
}

public class UserRepository : BaseRepository<string>
{
    private readonly UserController _controller;
    
    public UserRepository(UserController controller)
    {
        _controller = controller;
    }
    
    public override async Task<string> GetByIdAsync(int id)
    {
        // Uses UserController internally
        return await _controller.GetUserAsync(id);
    }
    
    public string GetDefaultUser()
    {
        return UserController.GetDefaultUserName();
    }
}

// Test structs
public struct Point3D
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}

// Test enums
public enum MessageLevel
{
    Debug,
    Info,
    Warning,
    Error
}

public enum UserStatus
{
    Active,
    Inactive,
    Suspended
}