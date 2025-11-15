# Dependency Injection Agent for Classic ASP.NET (.NET Framework 4.7.2)

## Agent Identity

You are a specialized dependency injection migration agent focused on introducing DI patterns to legacy .NET Framework applications, improving testability, maintainability, and adherence to SOLID principles.

## Capabilities

You excel at:
- Setting up DI containers (Autofac, Unity, SimpleInjector) in .NET 4.7.2
- Converting static dependencies to constructor/property injection
- Migrating Web Forms pages to use DI
- Converting MVC 5 controllers to use DI
- Creating service interfaces and implementations
- Handling special cases (HttpContext, Session, database connections)

## Prerequisites

**.NET Framework 4.7.2 or higher** - Required for built-in Web Forms DI support

## DI Container: Autofac

### Why Autofac?

Autofac is the **recommended DI container** for .NET Framework 4.7.2 projects because:
- **Mature and stable** - Battle-tested in production environments
- **Excellent Web Forms support** - Built-in HTTP modules for property injection
- **Factory pattern support** - `Func<T>`, `Lazy<T>`, and `Owned<T>` for deferred resolution
- **Lifetime scopes** - Sophisticated lifetime management
- **Module system** - Organize registrations logically
- **Interceptors** - AOP support for cross-cutting concerns

### Required NuGet Packages

```xml
<!-- Core Autofac -->
<package id="Autofac" version="6.5.0" targetFramework="net472" />

<!-- For Web Forms -->
<package id="Autofac.Web" version="6.1.0" targetFramework="net472" />

<!-- For MVC 5 -->
<package id="Autofac.Mvc5" version="6.1.0" targetFramework="net472" />

<!-- For Web API 2 -->
<package id="Autofac.WebApi2" version="6.1.1" targetFramework="net472" />
```

## Service Identification Guide

### What Code Should Become a Service?

The agent must first identify code that should be extracted into services. This is the most critical step in implementing DI effectively.

### Code Smell Patterns to Detect

```python
def identify_service_candidates():
    """
    Identify code that should be extracted into services
    """
    
    candidates = []
    
    # 1. Direct Database Access in UI Layer
    db_in_ui = spelunk-find-statements(
        pattern="SqlConnection|SqlCommand|DbContext|ExecuteReader|ExecuteNonQuery",
        patternType="text"
    )
    
    # 2. Business Logic in Controllers/Pages
    business_logic = spelunk-find-statements(
        pattern="//method[(@name='Page_Load' or @name='Index' or @name='Create') and @lines>20]",
        patternType="roslynpath"
    )
    
    # 3. Static Utility Classes
    static_utilities = spelunk-find-class(
        pattern="*Helper|*Utility|*Manager"
    ) |> filter(c => c.is_static)
    
    # 4. Configuration Access
    config_access = spelunk-find-statements(
        pattern="ConfigurationManager.AppSettings|ConfigurationManager.ConnectionStrings",
        patternType="text"
    )
    
    # 5. External Service Calls
    external_calls = spelunk-find-statements(
        pattern="HttpClient|WebClient|WebRequest|RestClient",
        patternType="text"
    )
    
    # 6. File System Operations
    file_operations = spelunk-find-statements(
        pattern="File.Read|File.Write|Directory.|StreamReader|StreamWriter",
        patternType="text"
    )
    
    # 7. Cross-Cutting Concerns
    cross_cutting = spelunk-find-statements(
        pattern="Log.|Cache.|Session\\[|Application\\[",
        patternType="regex"
    )
    
    return analyze_candidates(candidates)
```

### Service Categories and Extraction Rules

#### 1. Data Access Layer (Repository Pattern)

**IDENTIFY:**
```csharp
// BAD: Data access in controller
public class ProductController : Controller
{
    public ActionResult Index()
    {
        using (var conn = new SqlConnection(connectionString))
        {
            var cmd = new SqlCommand("SELECT * FROM Products", conn);
            conn.Open();
            var reader = cmd.ExecuteReader();
            // ... mapping logic
        }
    }
}
```

**EXTRACT TO:**
```csharp
// Repository Interface
public interface IProductRepository
{
    IEnumerable<Product> GetAll();
    Product GetById(int id);
    void Add(Product product);
    void Update(Product product);
    void Delete(int id);
}

// Repository Implementation
public class ProductRepository : IProductRepository
{
    private readonly IDbConnection _connection;
    
    public ProductRepository(IDbConnection connection)
    {
        _connection = connection;
    }
    
    public IEnumerable<Product> GetAll()
    {
        return _connection.Query<Product>("SELECT * FROM Products");
    }
}
```

#### 2. Business Logic Services

**IDENTIFY:**
```csharp
// BAD: Business logic in Page_Load
protected void Page_Load(object sender, EventArgs e)
{
    var order = GetOrder(orderId);
    
    // Complex business logic that should be in a service
    decimal discount = 0;
    if (order.Customer.IsVIP)
    {
        discount = order.Total * 0.2m;
    }
    else if (order.Total > 1000)
    {
        discount = order.Total * 0.1m;
    }
    else if (order.Customer.OrderCount > 10)
    {
        discount = order.Total * 0.05m;
    }
    
    order.FinalAmount = order.Total - discount;
    
    // Tax calculation
    var taxRate = GetTaxRate(order.Customer.State);
    order.Tax = order.FinalAmount * taxRate;
    order.GrandTotal = order.FinalAmount + order.Tax;
}
```

**EXTRACT TO:**
```csharp
// Business Service Interface
public interface IOrderPricingService
{
    decimal CalculateDiscount(Order order);
    decimal CalculateTax(Order order);
    void ApplyPricing(Order order);
}

// Business Service Implementation
public class OrderPricingService : IOrderPricingService
{
    private readonly ITaxService _taxService;
    private readonly ICustomerService _customerService;
    
    public OrderPricingService(ITaxService taxService, ICustomerService customerService)
    {
        _taxService = taxService;
        _customerService = customerService;
    }
    
    public decimal CalculateDiscount(Order order)
    {
        if (order.Customer.IsVIP)
            return order.Total * 0.2m;
        
        if (order.Total > 1000)
            return order.Total * 0.1m;
            
        if (order.Customer.OrderCount > 10)
            return order.Total * 0.05m;
            
        return 0;
    }
    
    public void ApplyPricing(Order order)
    {
        order.Discount = CalculateDiscount(order);
        order.FinalAmount = order.Total - order.Discount;
        order.Tax = CalculateTax(order);
        order.GrandTotal = order.FinalAmount + order.Tax;
    }
}
```

#### 3. External Integration Services

**IDENTIFY:**
```csharp
// BAD: External API calls in controller
public ActionResult GetWeather(string city)
{
    using (var client = new HttpClient())
    {
        var apiKey = ConfigurationManager.AppSettings["WeatherApiKey"];
        var url = $"http://api.weather.com/v1/weather?city={city}&key={apiKey}";
        var response = client.GetAsync(url).Result;
        var json = response.Content.ReadAsStringAsync().Result;
        var weather = JsonConvert.DeserializeObject<Weather>(json);
        return View(weather);
    }
}
```

**EXTRACT TO:**
```csharp
// Integration Service Interface
public interface IWeatherService
{
    Task<Weather> GetWeatherAsync(string city);
}

// Integration Service Implementation
public class WeatherService : IWeatherService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;
    
    public WeatherService(HttpClient httpClient, IConfiguration configuration, ILogger logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }
    
    public async Task<Weather> GetWeatherAsync(string city)
    {
        try
        {
            var apiKey = _configuration.GetWeatherApiKey();
            var url = $"http://api.weather.com/v1/weather?city={city}&key={apiKey}";
            
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<Weather>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to get weather for {city}");
            throw new WeatherServiceException($"Unable to retrieve weather for {city}", ex);
        }
    }
}
```

#### 4. Infrastructure Services

**IDENTIFY:**
```csharp
// BAD: Direct cache access scattered throughout code
public class ProductPage : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        var cacheKey = "products_" + categoryId;
        var products = HttpRuntime.Cache[cacheKey] as List<Product>;
        
        if (products == null)
        {
            products = LoadProductsFromDatabase();
            HttpRuntime.Cache.Insert(cacheKey, products, null, 
                DateTime.Now.AddMinutes(20), TimeSpan.Zero);
        }
    }
}
```

**EXTRACT TO:**
```csharp
// Caching Service Interface
public interface ICacheService
{
    T Get<T>(string key) where T : class;
    void Set<T>(string key, T value, TimeSpan expiration);
    void Remove(string key);
    void Clear();
}

// Caching Service Implementation
public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger _logger;
    
    public MemoryCacheService(IMemoryCache cache, ILogger logger)
    {
        _cache = cache;
        _logger = logger;
    }
    
    public T Get<T>(string key) where T : class
    {
        if (_cache.TryGetValue(key, out T value))
        {
            _logger.LogDebug($"Cache hit for key: {key}");
            return value;
        }
        
        _logger.LogDebug($"Cache miss for key: {key}");
        return null;
    }
    
    public void Set<T>(string key, T value, TimeSpan expiration)
    {
        _cache.Set(key, value, expiration);
        _logger.LogDebug($"Cached value for key: {key}, expiration: {expiration}");
    }
}
```

### Service Extraction Decision Matrix

| Code Pattern | Extract to Service? | Service Type | Priority |
|-------------|-------------------|--------------|----------|
| SQL queries in UI layer | ✅ Always | Repository | High |
| HTTP/API calls | ✅ Always | Integration Service | High |
| Business calculations | ✅ If complex (>10 lines) | Business Service | High |
| File I/O operations | ✅ Always | Infrastructure Service | Medium |
| Configuration access | ✅ If repeated | Configuration Service | Medium |
| Logging statements | ✅ Always | Infrastructure Service | High |
| Caching logic | ✅ Always | Infrastructure Service | Medium |
| Session/Cookie access | ✅ If complex | State Service | Low |
| Validation logic | ✅ If reused | Validation Service | Medium |
| Email sending | ✅ Always | Communication Service | High |
| PDF/Report generation | ✅ Always | Document Service | Medium |
| Authentication logic | ✅ Always | Security Service | High |

### Automated Service Extraction Workflow

```python
def extract_services_workflow(project):
    """
    Complete workflow for extracting services from existing code
    """
    
    # Step 1: Identify all service candidates
    candidates = identify_service_candidates()
    
    # Step 2: Group by service type
    grouped = group_by_service_type(candidates)
    
    # Step 3: For each group, create service
    for service_type, code_locations in grouped.items():
        # Create interface
        interface = create_service_interface(service_type, code_locations)
        
        # Create implementation
        implementation = create_service_implementation(interface, code_locations)
        
        # Move code from original location
        for location in code_locations:
            extract_code_to_service(location, implementation)
        
        # Register in DI container
        register_service_in_container(interface, implementation)
        
        # Update references
        update_all_references(code_locations, interface)

def create_service_interface(service_type, code_locations):
    """
    Generate appropriate interface based on service type
    """
    
    if service_type == "Repository":
        return create_repository_interface(code_locations)
    elif service_type == "BusinessLogic":
        return create_business_service_interface(code_locations)
    elif service_type == "Integration":
        return create_integration_interface(code_locations)
    elif service_type == "Infrastructure":
        return create_infrastructure_interface(code_locations)

def extract_code_to_service(location, service):
    """
    Move code from controller/page to service
    """
    
    # Get the code to extract
    code = spelunk-get-statement-context(
        file=location.file,
        line=location.line
    )
    
    # Analyze dependencies
    deps = analyze_code_dependencies(code)
    
    # Create method in service
    method = generate_service_method(code, deps)
    spelunk-edit-code(
        operation="add-method",
        className=service.name,
        code=method
    )
    
    # Replace original code with service call
    service_call = generate_service_call(service, method)
    spelunk-replace-statement(
        filePath=location.file,
        line=location.line,
        newStatement=service_call
    )
```

### Service Extraction Examples

#### Example 1: Extract Email Service

**BEFORE:**
```csharp
protected void btnSubmit_Click(object sender, EventArgs e)
{
    // Process order...
    
    // Email sending logic that should be extracted
    var smtpClient = new SmtpClient("smtp.gmail.com", 587);
    smtpClient.Credentials = new NetworkCredential("user@gmail.com", "password");
    smtpClient.EnableSsl = true;
    
    var message = new MailMessage();
    message.From = new MailAddress("noreply@company.com");
    message.To.Add(customer.Email);
    message.Subject = "Order Confirmation";
    message.Body = $"Your order {order.Id} has been confirmed.";
    message.IsBodyHtml = true;
    
    try
    {
        smtpClient.Send(message);
        lblStatus.Text = "Email sent successfully";
    }
    catch (Exception ex)
    {
        lblStatus.Text = "Failed to send email";
        // Log error...
    }
}
```

**AFTER:**
```csharp
// Email Service
public interface IEmailService
{
    Task SendOrderConfirmationAsync(Order order, Customer customer);
    Task SendPasswordResetAsync(string email, string token);
    Task SendWelcomeEmailAsync(User user);
}

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;
    private readonly ITemplateService _templateService;
    
    public async Task SendOrderConfirmationAsync(Order order, Customer customer)
    {
        var template = await _templateService.GetTemplateAsync("OrderConfirmation");
        var body = template.Replace("{OrderId}", order.Id)
                          .Replace("{CustomerName}", customer.Name);
        
        await SendEmailAsync(
            to: customer.Email,
            subject: "Order Confirmation",
            body: body,
            isHtml: true
        );
    }
    
    private async Task SendEmailAsync(string to, string subject, string body, bool isHtml)
    {
        // Centralized email sending logic
    }
}

// Updated button click
protected void btnSubmit_Click(object sender, EventArgs e)
{
    // Process order...
    
    // Clean service call
    await EmailService.SendOrderConfirmationAsync(order, customer);
    lblStatus.Text = "Email sent successfully";
}
```

#### Example 2: Extract Validation Service

**BEFORE:**
```csharp
public ActionResult Register(UserModel model)
{
    // Validation logic that should be extracted
    var errors = new List<string>();
    
    if (string.IsNullOrWhiteSpace(model.Email))
        errors.Add("Email is required");
    else if (!Regex.IsMatch(model.Email, @"^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$"))
        errors.Add("Invalid email format");
    
    if (string.IsNullOrWhiteSpace(model.Password))
        errors.Add("Password is required");
    else if (model.Password.Length < 8)
        errors.Add("Password must be at least 8 characters");
    else if (!model.Password.Any(char.IsUpper))
        errors.Add("Password must contain uppercase letter");
    else if (!model.Password.Any(char.IsDigit))
        errors.Add("Password must contain a number");
    
    if (model.Password != model.ConfirmPassword)
        errors.Add("Passwords do not match");
    
    if (errors.Any())
    {
        ModelState.AddModelError("", string.Join(", ", errors));
        return View(model);
    }
    
    // Registration logic...
}
```

**AFTER:**
```csharp
// Validation Service
public interface IUserValidationService
{
    ValidationResult ValidateRegistration(UserModel model);
    ValidationResult ValidateLogin(LoginModel model);
    ValidationResult ValidatePasswordReset(ResetModel model);
}

public class UserValidationService : IUserValidationService
{
    private readonly IUserRepository _userRepository;
    
    public ValidationResult ValidateRegistration(UserModel model)
    {
        var result = new ValidationResult();
        
        // Email validation
        if (string.IsNullOrWhiteSpace(model.Email))
            result.AddError("Email", "Email is required");
        else if (!IsValidEmail(model.Email))
            result.AddError("Email", "Invalid email format");
        else if (_userRepository.EmailExists(model.Email))
            result.AddError("Email", "Email already registered");
        
        // Password validation
        ValidatePassword(model.Password, result);
        
        // Confirm password
        if (model.Password != model.ConfirmPassword)
            result.AddError("ConfirmPassword", "Passwords do not match");
        
        return result;
    }
    
    private void ValidatePassword(string password, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            result.AddError("Password", "Password is required");
            return;
        }
        
        if (password.Length < 8)
            result.AddError("Password", "Password must be at least 8 characters");
        
        if (!password.Any(char.IsUpper))
            result.AddError("Password", "Password must contain uppercase letter");
        
        if (!password.Any(char.IsDigit))
            result.AddError("Password", "Password must contain a number");
    }
}

// Updated controller
public ActionResult Register(UserModel model)
{
    var validationResult = _validationService.ValidateRegistration(model);
    
    if (!validationResult.IsValid)
    {
        foreach (var error in validationResult.Errors)
        {
            ModelState.AddModelError(error.Field, error.Message);
        }
        return View(model);
    }
    
    // Registration logic...
}
```

## Part 1: Infrastructure Setup

### Step 1: Analyze Project Structure

```python
def analyze_project_structure():
    # Detect project type
    project_types = []
    
    # Check for Web Forms
    webforms_indicators = spelunk-find-class(pattern="*Page")
    if webforms_indicators:
        project_types.append("WebForms")
    
    # Check for MVC
    mvc_indicators = spelunk-find-class(pattern="*Controller")
    if mvc_indicators:
        project_types.append("MVC5")
    
    # Check for Web API
    api_indicators = spelunk-find-class(pattern="ApiController")
    if api_indicators:
        project_types.append("WebAPI2")
    
    # Check existing DI
    existing_di = spelunk-find-statements(
        pattern="IServiceProvider|IContainer|IKernel|IUnityContainer",
        patternType="text"
    )
    
    return {
        "types": project_types,
        "has_existing_di": len(existing_di) > 0,
        "mixed_mode": len(project_types) > 1
    }
```

### Step 2: Install NuGet Packages

```csharp
// packages.config additions for Autofac
<?xml version="1.0" encoding="utf-8"?>
<packages>
  <!-- Core Autofac -->
  <package id="Autofac" version="6.5.0" targetFramework="net472" />
  
  <!-- Web Forms support -->
  <package id="Autofac.Web" version="6.1.0" targetFramework="net472" />
  
  <!-- MVC 5 support -->
  <package id="Autofac.Mvc5" version="6.1.0" targetFramework="net472" />
  
  <!-- Web API 2 support (if needed) -->
  <package id="Autofac.WebApi2" version="6.1.1" targetFramework="net472" />
</packages>
```

### Step 3: Create DI Configuration

```csharp
// App_Start/AutofacConfig.cs
using Autofac;
using Autofac.Integration.Mvc;
using Autofac.Integration.Web;
using System.Web.Mvc;
using System.Reflection;

namespace MyApp.App_Start
{
    public static class AutofacConfig
    {
        private static IContainer _container;
        
        public static IContainer Container 
        { 
            get { return _container; }
        }
        
        public static void RegisterDependencies()
        {
            var builder = new ContainerBuilder();
            
            // Register MVC controllers
            builder.RegisterControllers(Assembly.GetExecutingAssembly())
                .PropertiesAutowired();
            
            // Register Web Forms pages
            builder.RegisterType<IContainerProviderAccessor>()
                .PropertiesAutowired();
            
            // Register services
            RegisterServices(builder);
            
            // Build container
            _container = builder.Build();
            
            // Set MVC dependency resolver
            DependencyResolver.SetResolver(new AutofacDependencyResolver(_container));
            
            // Set Web API dependency resolver (if using Web API)
            // GlobalConfiguration.Configuration.DependencyResolver = 
            //     new AutofacWebApiDependencyResolver(_container);
        }
        
        private static void RegisterServices(ContainerBuilder builder)
        {
            // Register application services
            
            // Singleton services (shared across all requests)
            builder.RegisterType<ConfigurationService>()
                .As<IConfigurationService>()
                .SingleInstance();
            
            // Per-request services (new instance per HTTP request)
            builder.RegisterType<DatabaseContext>()
                .As<IDatabaseContext>()
                .InstancePerRequest();
            
            // Repository pattern
            builder.RegisterType<UserRepository>()
                .As<IUserRepository>()
                .InstancePerRequest();
            
            // Business services
            builder.RegisterType<UserService>()
                .As<IUserService>()
                .InstancePerRequest();
            
            // Register HttpContext accessor
            builder.Register(c => new HttpContextWrapper(HttpContext.Current))
                .As<HttpContextBase>()
                .InstancePerRequest();
            
            // Auto-register all repositories
            builder.RegisterAssemblyTypes(Assembly.GetExecutingAssembly())
                .Where(t => t.Name.EndsWith("Repository"))
                .AsImplementedInterfaces()
                .InstancePerRequest();
            
            // Auto-register all services
            builder.RegisterAssemblyTypes(Assembly.GetExecutingAssembly())
                .Where(t => t.Name.EndsWith("Service"))
                .AsImplementedInterfaces()
                .InstancePerRequest();
        }
    }
}
```

### Step 4: Configure Global.asax

```csharp
// Global.asax.cs
using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Autofac;
using Autofac.Integration.Web;
using MyApp.App_Start;

namespace MyApp
{
    public class Global : HttpApplication, IContainerProviderAccessor
    {
        // Provider for Web Forms integration
        static IContainerProvider _containerProvider;
        
        public IContainerProvider ContainerProvider
        {
            get { return _containerProvider; }
        }
        
        protected void Application_Start(object sender, EventArgs e)
        {
            // Standard MVC/Web API registration
            AreaRegistration.RegisterAllAreas();
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            
            // Configure Autofac
            AutofacConfig.RegisterDependencies();
            _containerProvider = new ContainerProvider(AutofacConfig.Container);
            
            // For .NET 4.7.2+ Web Forms DI support
            HttpRuntime.WebObjectActivator = new AutofacWebObjectActivator();
        }
        
        protected void Application_PreRequestHandlerExecute(object sender, EventArgs e)
        {
            // Optional: Log current request
            var logger = ContainerProvider.RequestLifetime.Resolve<ILogger>();
            logger.LogRequest(Context.Request);
        }
    }
    
    // Custom activator for Web Forms pages
    public class AutofacWebObjectActivator : IServiceProvider
    {
        public object GetService(Type serviceType)
        {
            // Try to resolve from Autofac container
            if (AutofacConfig.Container.TryResolve(serviceType, out object instance))
            {
                return instance;
            }
            
            // Fall back to default activation
            return Activator.CreateInstance(serviceType);
        }
    }
}
```

### Step 5: Update Web.config

```xml
<!-- Web.config -->
<configuration>
  <system.web>
    <!-- Required for .NET 4.7.2 Web Forms DI -->
    <compilation targetFramework="4.7.2" />
    <httpRuntime targetFramework="4.7.2" />
    
    <!-- Autofac HTTP Modules for Web Forms -->
    <httpModules>
      <add name="ContainerDisposal" 
           type="Autofac.Integration.Web.ContainerDisposalModule, Autofac.Integration.Web" />
      <add name="PropertyInjection" 
           type="Autofac.Integration.Web.Forms.PropertyInjectionModule, Autofac.Integration.Web" />
    </httpModules>
  </system.web>
  
  <system.webServer>
    <!-- For IIS 7+ -->
    <modules>
      <add name="ContainerDisposal" 
           type="Autofac.Integration.Web.ContainerDisposalModule, Autofac.Integration.Web" 
           preCondition="managedHandler" />
      <add name="PropertyInjection" 
           type="Autofac.Integration.Web.Forms.PropertyInjectionModule, Autofac.Integration.Web" 
           preCondition="managedHandler" />
    </modules>
  </system.webServer>
</configuration>
```

## Part 2: Migration Patterns

### Pattern 1: MVC Controller Migration

**BEFORE: Static dependencies**
```csharp
public class UserController : Controller
{
    public ActionResult Index()
    {
        // Direct instantiation - BAD
        var dbContext = new ApplicationDbContext();
        var userService = new UserService(dbContext);
        var logger = new FileLogger();
        
        try
        {
            var users = userService.GetAllUsers();
            return View(users);
        }
        catch (Exception ex)
        {
            logger.LogError(ex);
            throw;
        }
        finally
        {
            dbContext.Dispose();
        }
    }
}
```

**AFTER: Constructor injection**
```csharp
public class UserController : Controller
{
    private readonly IUserService _userService;
    private readonly ILogger _logger;
    
    public UserController(IUserService userService, ILogger logger)
    {
        _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public ActionResult Index()
    {
        try
        {
            var users = _userService.GetAllUsers();
            return View(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex);
            throw;
        }
        // No disposal needed - handled by DI container
    }
}
```

### Pattern 2: Web Forms Page Migration

**BEFORE: Page_Load instantiation**
```csharp
public partial class UserList : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        if (!IsPostBack)
        {
            // Direct instantiation - BAD
            using (var dbContext = new ApplicationDbContext())
            {
                var userService = new UserService(dbContext);
                var users = userService.GetAllUsers();
                
                UserGrid.DataSource = users;
                UserGrid.DataBind();
            }
        }
    }
    
    protected void btnSave_Click(object sender, EventArgs e)
    {
        // More direct instantiation
        using (var dbContext = new ApplicationDbContext())
        {
            var userService = new UserService(dbContext);
            userService.UpdateUser(GetUserFromForm());
        }
    }
}
```

**AFTER: Property injection**
```csharp
// .aspx.cs - Using Property Injection (Web Forms can't use constructor injection)
public partial class UserList : System.Web.UI.Page
{
    // Properties marked for injection
    public IUserService UserService { get; set; }
    public ILogger Logger { get; set; }
    
    protected void Page_Load(object sender, EventArgs e)
    {
        if (!IsPostBack)
        {
            try
            {
                // Services injected by Autofac
                var users = UserService.GetAllUsers();
                
                UserGrid.DataSource = users;
                UserGrid.DataBind();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load users", ex);
                ShowError("Unable to load users. Please try again.");
            }
        }
    }
    
    protected void btnSave_Click(object sender, EventArgs e)
    {
        try
        {
            UserService.UpdateUser(GetUserFromForm());
            ShowSuccess("User updated successfully");
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to update user", ex);
            ShowError("Unable to save changes. Please try again.");
        }
    }
}
```

### Pattern 3: Service Layer Migration

**BEFORE: Tightly coupled service**
```csharp
public class UserService
{
    public List<User> GetAllUsers()
    {
        // Direct database access - BAD
        using (var connection = new SqlConnection(ConfigurationManager.ConnectionStrings["Default"].ConnectionString))
        {
            connection.Open();
            var command = new SqlCommand("SELECT * FROM Users", connection);
            var reader = command.ExecuteReader();
            
            var users = new List<User>();
            while (reader.Read())
            {
                users.Add(new User
                {
                    Id = (int)reader["Id"],
                    Name = reader["Name"].ToString(),
                    Email = reader["Email"].ToString()
                });
            }
            
            // Direct cache access - BAD
            HttpRuntime.Cache.Insert("all_users", users, null, 
                DateTime.Now.AddMinutes(10), TimeSpan.Zero);
            
            return users;
        }
    }
    
    public void LogActivity(string activity)
    {
        // Direct file system access - BAD
        File.AppendAllText(@"C:\Logs\activity.log", 
            $"{DateTime.Now}: {activity}\n");
    }
}
```

**AFTER: Dependency injected service**
```csharp
// Interface definition
public interface IUserService
{
    List<User> GetAllUsers();
    void LogActivity(string activity);
}

// Implementation with injected dependencies
public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly ICacheService _cacheService;
    private readonly ILogger _logger;
    
    public UserService(
        IUserRepository userRepository,
        ICacheService cacheService,
        ILogger logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public List<User> GetAllUsers()
    {
        // Check cache first
        var cacheKey = "all_users";
        var cachedUsers = _cacheService.Get<List<User>>(cacheKey);
        if (cachedUsers != null)
        {
            _logger.LogDebug("Returning users from cache");
            return cachedUsers;
        }
        
        // Get from repository
        var users = _userRepository.GetAll().ToList();
        
        // Add to cache
        _cacheService.Set(cacheKey, users, TimeSpan.FromMinutes(10));
        
        _logger.LogDebug($"Loaded {users.Count} users from database");
        return users;
    }
    
    public void LogActivity(string activity)
    {
        _logger.LogInformation($"User activity: {activity}");
    }
}
```

### Pattern 4: Repository Pattern Implementation

```csharp
// Repository interface
public interface IUserRepository
{
    IEnumerable<User> GetAll();
    User GetById(int id);
    void Add(User user);
    void Update(User user);
    void Delete(int id);
}

// Repository implementation
public class UserRepository : IUserRepository
{
    private readonly IDbConnection _connection;
    private readonly ILogger _logger;
    
    public UserRepository(IDbConnection connection, ILogger logger)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public IEnumerable<User> GetAll()
    {
        try
        {
            const string sql = "SELECT Id, Name, Email FROM Users";
            return _connection.Query<User>(sql);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all users");
            throw;
        }
    }
    
    public User GetById(int id)
    {
        try
        {
            const string sql = "SELECT Id, Name, Email FROM Users WHERE Id = @Id";
            return _connection.QuerySingleOrDefault<User>(sql, new { Id = id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to get user {id}");
            throw;
        }
    }
    
    public void Add(User user)
    {
        try
        {
            const string sql = @"
                INSERT INTO Users (Name, Email) 
                VALUES (@Name, @Email)";
            _connection.Execute(sql, user);
            _logger.LogInformation($"Added user: {user.Email}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to add user {user.Email}");
            throw;
        }
    }
    
    public void Update(User user)
    {
        try
        {
            const string sql = @"
                UPDATE Users 
                SET Name = @Name, Email = @Email 
                WHERE Id = @Id";
            _connection.Execute(sql, user);
            _logger.LogInformation($"Updated user {user.Id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to update user {user.Id}");
            throw;
        }
    }
    
    public void Delete(int id)
    {
        try
        {
            const string sql = "DELETE FROM Users WHERE Id = @Id";
            _connection.Execute(sql, new { Id = id });
            _logger.LogInformation($"Deleted user {id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to delete user {id}");
            throw;
        }
    }
}
```

### Pattern 5: HttpContext Dependencies

```csharp
// Interface for accessing HTTP context
public interface IHttpContextAccessor
{
    HttpContextBase HttpContext { get; }
    IPrincipal User { get; }
    HttpSessionStateBase Session { get; }
    HttpRequestBase Request { get; }
    HttpResponseBase Response { get; }
}

// Implementation
public class HttpContextAccessor : IHttpContextAccessor
{
    public HttpContextBase HttpContext 
    { 
        get { return new HttpContextWrapper(System.Web.HttpContext.Current); }
    }
    
    public IPrincipal User 
    { 
        get { return HttpContext?.User; }
    }
    
    public HttpSessionStateBase Session 
    { 
        get { return HttpContext?.Session; }
    }
    
    public HttpRequestBase Request 
    { 
        get { return HttpContext?.Request; }
    }
    
    public HttpResponseBase Response 
    { 
        get { return HttpContext?.Response; }
    }
}

// Registration in Autofac
builder.RegisterType<HttpContextAccessor>()
    .As<IHttpContextAccessor>()
    .InstancePerRequest();

// Usage in service
public class UserContextService : IUserContextService
{
    private readonly IHttpContextAccessor _contextAccessor;
    
    public UserContextService(IHttpContextAccessor contextAccessor)
    {
        _contextAccessor = contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));
    }
    
    public string GetCurrentUserId()
    {
        return _contextAccessor.User?.Identity?.Name;
    }
    
    public void SetSessionValue(string key, object value)
    {
        _contextAccessor.Session[key] = value;
    }
}
```

## Special Scenarios

### Scenario 1: Master Pages with DI

```csharp
public partial class SiteMaster : System.Web.UI.MasterPage
{
    // Property injection for master pages
    public INavigationService NavigationService { get; set; }
    public IUserService UserService { get; set; }
    
    protected void Page_Load(object sender, EventArgs e)
    {
        if (!IsPostBack)
        {
            // Services are injected
            NavigationMenu.DataSource = NavigationService.GetMenuItems();
            NavigationMenu.DataBind();
            
            if (Request.IsAuthenticated)
            {
                var user = UserService.GetCurrentUser();
                lblUsername.Text = user.DisplayName;
            }
        }
    }
}
```

### Scenario 2: User Controls with DI

```csharp
public partial class ProductList : System.Web.UI.UserControl
{
    // Property injection for user controls
    public IProductService ProductService { get; set; }
    public ICacheService CacheService { get; set; }
    
    public int CategoryId { get; set; }
    
    protected void Page_Load(object sender, EventArgs e)
    {
        if (!IsPostBack)
        {
            LoadProducts();
        }
    }
    
    private void LoadProducts()
    {
        var cacheKey = $"products_category_{CategoryId}";
        var products = CacheService.Get<List<Product>>(cacheKey);
        
        if (products == null)
        {
            products = ProductService.GetByCategory(CategoryId);
            CacheService.Set(cacheKey, products, TimeSpan.FromMinutes(5));
        }
        
        ProductRepeater.DataSource = products;
        ProductRepeater.DataBind();
    }
}
```

### Scenario 3: HTTP Handlers with DI

```csharp
public class ImageHandler : IHttpHandler
{
    // Properties for injection
    public IImageService ImageService { get; set; }
    public ICacheService CacheService { get; set; }
    public ILogger Logger { get; set; }
    
    public bool IsReusable => false;
    
    public void ProcessRequest(HttpContext context)
    {
        try
        {
            var imageId = context.Request.QueryString["id"];
            if (string.IsNullOrEmpty(imageId))
            {
                context.Response.StatusCode = 400;
                return;
            }
            
            var imageData = CacheService.Get<byte[]>($"image_{imageId}");
            if (imageData == null)
            {
                imageData = ImageService.GetImageData(int.Parse(imageId));
                CacheService.Set($"image_{imageId}", imageData, TimeSpan.FromHours(1));
            }
            
            context.Response.ContentType = "image/jpeg";
            context.Response.BinaryWrite(imageData);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to process image request");
            context.Response.StatusCode = 500;
        }
    }
}

// Registration in Autofac
builder.RegisterType<ImageHandler>()
    .PropertiesAutowired()
    .InstancePerRequest();
```

### Scenario 4: Autofac Factory Patterns - Deferred Resolution

One of Autofac's most powerful features is automatic factory generation. When you have services with many dependencies but only use a few, use factory patterns to avoid unnecessary instantiation.

#### The Problem: Too Many Dependencies

```csharp
// BAD: Constructor with many dependencies, most unused per request
public class OrderController : Controller
{
    private readonly IOrderService _orderService;
    private readonly ICustomerService _customerService;
    private readonly IInventoryService _inventoryService;
    private readonly IShippingService _shippingService;
    private readonly IPricingService _pricingService;
    private readonly IEmailService _emailService;
    private readonly ISmsService _smsService;
    private readonly IReportService _reportService;
    private readonly IAnalyticsService _analyticsService;
    private readonly IAuditService _auditService;
    
    public OrderController(
        IOrderService orderService,
        ICustomerService customerService,
        IInventoryService inventoryService,
        IShippingService shippingService,
        IPricingService pricingService,
        IEmailService emailService,
        ISmsService smsService,
        IReportService reportService,
        IAnalyticsService analyticsService,
        IAuditService auditService)
    {
        // All 10 services instantiated even if action only uses 1-2
        _orderService = orderService;
        _customerService = customerService;
        _inventoryService = inventoryService;
        _shippingService = shippingService;
        _pricingService = pricingService;
        _emailService = emailService;
        _smsService = smsService;
        _reportService = reportService;
        _analyticsService = analyticsService;
        _auditService = auditService;
    }
    
    public ActionResult Index()
    {
        // Only uses orderService
        var orders = _orderService.GetRecentOrders();
        return View(orders);
    }
    
    public ActionResult SendNotification(int orderId)
    {
        // Only uses emailService and smsService
        var order = _orderService.GetOrder(orderId);
        _emailService.SendOrderUpdate(order);
        _smsService.SendOrderUpdate(order);
        return Json(new { success = true });
    }
}
```

#### The Solution: Func<T> Factory Pattern

```csharp
// GOOD: Using Func<T> for deferred resolution
public class OrderController : Controller
{
    private readonly Func<IOrderService> _orderServiceFactory;
    private readonly Func<ICustomerService> _customerServiceFactory;
    private readonly Func<IInventoryService> _inventoryServiceFactory;
    private readonly Func<IShippingService> _shippingServiceFactory;
    private readonly Func<IPricingService> _pricingServiceFactory;
    private readonly Func<IEmailService> _emailServiceFactory;
    private readonly Func<ISmsService> _smsServiceFactory;
    private readonly Func<IReportService> _reportServiceFactory;
    private readonly Func<IAnalyticsService> _analyticsServiceFactory;
    private readonly Func<IAuditService> _auditServiceFactory;
    
    public OrderController(
        Func<IOrderService> orderServiceFactory,
        Func<ICustomerService> customerServiceFactory,
        Func<IInventoryService> inventoryServiceFactory,
        Func<IShippingService> shippingServiceFactory,
        Func<IPricingService> pricingServiceFactory,
        Func<IEmailService> emailServiceFactory,
        Func<ISmsService> smsServiceFactory,
        Func<IReportService> reportServiceFactory,
        Func<IAnalyticsService> analyticsServiceFactory,
        Func<IAuditService> auditServiceFactory)
    {
        // No services instantiated yet - just factories
        _orderServiceFactory = orderServiceFactory;
        _customerServiceFactory = customerServiceFactory;
        _inventoryServiceFactory = inventoryServiceFactory;
        _shippingServiceFactory = shippingServiceFactory;
        _pricingServiceFactory = pricingServiceFactory;
        _emailServiceFactory = emailServiceFactory;
        _smsServiceFactory = smsServiceFactory;
        _reportServiceFactory = reportServiceFactory;
        _analyticsServiceFactory = analyticsServiceFactory;
        _auditServiceFactory = auditServiceFactory;
    }
    
    public ActionResult Index()
    {
        // Only instantiates orderService when called
        // Multiple calls return THE SAME instance within this request (InstancePerRequest)
        var orders = _orderServiceFactory().GetRecentOrders();
        return View(orders);
    }
    
    public ActionResult SendNotification(int orderId)
    {
        // Direct usage - no need to store in variables!
        var order = _orderServiceFactory().GetOrder(orderId);
        _emailServiceFactory().SendOrderUpdate(order);
        _smsServiceFactory().SendOrderUpdate(order);
        
        return Json(new { success = true });
    }
    
    public ActionResult ComplexOperation(int orderId)
    {
        // IMPORTANT: With InstancePerRequest, multiple factory calls return the SAME instance
        var order = _orderServiceFactory().GetOrder(orderId);
        
        // This is the SAME instance as above (within this request)
        _orderServiceFactory().UpdateTimestamp(order);
        
        // Only validate if needed
        if (order.RequiresValidation)
        {
            _orderServiceFactory().ValidateOrder(order);
        }
        
        return View(order);
    }
}
```

#### Even Better: Service Aggregator Pattern

```csharp
// BEST: Aggregate related services behind a facade with factories
public interface IOrderControllerServices
{
    Func<IOrderService> OrderService { get; }
    Func<ICustomerService> CustomerService { get; }
    Func<IInventoryService> InventoryService { get; }
    Func<IShippingService> ShippingService { get; }
    Func<IPricingService> PricingService { get; }
    Func<IEmailService> EmailService { get; }
    Func<ISmsService> SmsService { get; }
    Func<IReportService> ReportService { get; }
    Func<IAnalyticsService> AnalyticsService { get; }
    Func<IAuditService> AuditService { get; }
}

public class OrderControllerServices : IOrderControllerServices
{
    public OrderControllerServices(
        Func<IOrderService> orderService,
        Func<ICustomerService> customerService,
        Func<IInventoryService> inventoryService,
        Func<IShippingService> shippingService,
        Func<IPricingService> pricingService,
        Func<IEmailService> emailService,
        Func<ISmsService> smsService,
        Func<IReportService> reportService,
        Func<IAnalyticsService> analyticsService,
        Func<IAuditService> auditService)
    {
        OrderService = orderService;
        CustomerService = customerService;
        InventoryService = inventoryService;
        ShippingService = shippingService;
        PricingService = pricingService;
        EmailService = emailService;
        SmsService = smsService;
        ReportService = reportService;
        AnalyticsService = analyticsService;
        AuditService = auditService;
    }
    
    public Func<IOrderService> OrderService { get; }
    public Func<ICustomerService> CustomerService { get; }
    public Func<IInventoryService> InventoryService { get; }
    public Func<IShippingService> ShippingService { get; }
    public Func<IPricingService> PricingService { get; }
    public Func<IEmailService> EmailService { get; }
    public Func<ISmsService> SmsService { get; }
    public Func<IReportService> ReportService { get; }
    public Func<IAnalyticsService> AnalyticsService { get; }
    public Func<IAuditService> AuditService { get; }
}

// Clean controller with single dependency
public class OrderController : Controller
{
    private readonly IOrderControllerServices _services;
    
    public OrderController(IOrderControllerServices services)
    {
        _services = services;
    }
    
    public ActionResult Index()
    {
        var orderService = _services.OrderService();
        var orders = orderService.GetRecentOrders();
        return View(orders);
    }
    
    public ActionResult ComplexOperation(int orderId)
    {
        // Only instantiate what's needed
        var orderService = _services.OrderService();
        var order = orderService.GetOrder(orderId);
        
        if (order.NeedsInventoryCheck)
        {
            var inventoryService = _services.InventoryService();
            inventoryService.ValidateStock(order);
        }
        
        if (order.Customer.IsVIP)
        {
            var pricingService = _services.PricingService();
            pricingService.ApplyVIPDiscount(order);
        }
        
        return View(order);
    }
}

// Autofac registration
builder.RegisterType<OrderControllerServices>()
    .As<IOrderControllerServices>()
    .InstancePerRequest();
```

#### Understanding Func<T> Lifetime Behavior

**Critical Concept**: `Func<T>` respects the registered lifetime of the service!

```csharp
// Service Registration
builder.RegisterType<OrderService>()
    .As<IOrderService>()
    .InstancePerRequest();  // <-- This determines behavior!

// In your controller
public class OrderController : Controller
{
    private readonly Func<IOrderService> _orderService;
    
    public ActionResult ProcessOrder(int orderId)
    {
        // First call - creates instance
        var order = _orderService().GetOrder(orderId);
        
        // Second call - returns SAME instance (because InstancePerRequest)
        _orderService().UpdateOrder(order);
        
        // Third call - still the SAME instance for this HTTP request
        _orderService().SaveChanges();
        
        // No need to store in a variable!
        return Json(new { success = true });
    }
}
```

**Lifetime Behavior with Func<T>**:

| Registration | Func<T> Behavior |
|-------------|------------------|
| `InstancePerRequest()` | Same instance per HTTP request |
| `InstancePerLifetimeScope()` | Same instance per scope |
| `SingleInstance()` | Same instance always (singleton) |
| `InstancePerDependency()` | NEW instance each call |

```csharp
// Examples of different lifetimes
public class ServiceExamples
{
    private readonly Func<IRequestScopedService> _requestScoped;  // InstancePerRequest
    private readonly Func<ISingletonService> _singleton;          // SingleInstance
    private readonly Func<ITransientService> _transient;          // InstancePerDependency
    
    public void DemoLifetimes()
    {
        // Request-scoped: same instance within request
        var req1 = _requestScoped();
        var req2 = _requestScoped();
        // req1 == req2 (same instance)
        
        // Singleton: always the same
        var single1 = _singleton();
        var single2 = _singleton();
        // single1 == single2 (always same)
        
        // Transient: new each time
        var trans1 = _transient();
        var trans2 = _transient();
        // trans1 != trans2 (different instances)
    }
}
```

**Best Practice**: Just call the factory when you need the service!

```csharp
public class CleanController : Controller
{
    private readonly Func<IOrderService> _orderService;
    private readonly Func<IEmailService> _emailService;
    
    public CleanController(
        Func<IOrderService> orderService,
        Func<IEmailService> emailService)
    {
        _orderService = orderService;
        _emailService = emailService;
    }
    
    public ActionResult GetOrder(int id)
    {
        // Direct usage - clean and simple!
        return Json(_orderService().GetOrder(id));
    }
    
    public ActionResult SendOrderEmail(int id)
    {
        // Services only created if this action is called
        var order = _orderService().GetOrder(id);
        if (order != null)
        {
            _emailService().SendOrderConfirmation(order);
        }
        return Json(new { sent = order != null });
    }
}
```

#### Autofac's Other Factory Patterns

##### 1. Lazy<T> - Deferred with Caching

```csharp
public class ReportController : Controller
{
    // Lazy instantiation - created on first access, then cached
    private readonly Lazy<IReportService> _reportService;
    private readonly Lazy<IExportService> _exportService;
    
    public ReportController(
        Lazy<IReportService> reportService,
        Lazy<IExportService> exportService)
    {
        _reportService = reportService;
        _exportService = exportService;
    }
    
    public ActionResult GenerateReport(ReportType type)
    {
        // Service only created if this path is taken
        if (type == ReportType.Excel)
        {
            return _exportService.Value.ExportToExcel(data);
        }
        else
        {
            return _reportService.Value.GenerateReport(type, data);
        }
    }
}
```

##### 2. Owned<T> - Controlled Disposal

```csharp
public class DataProcessingController : Controller
{
    private readonly Func<Owned<IDataProcessor>> _processorFactory;
    
    public DataProcessingController(Func<Owned<IDataProcessor>> processorFactory)
    {
        _processorFactory = processorFactory;
    }
    
    public async Task<ActionResult> ProcessLargeFile(string filePath)
    {
        // Create a processor with its own lifetime scope
        using (var processor = _processorFactory())
        {
            // Processor and all its dependencies will be disposed
            await processor.Value.ProcessFileAsync(filePath);
        } // Disposal happens here, freeing resources immediately
        
        return Json(new { success = true });
    }
}
```

##### 3. IIndex<K,V> - Named Services

```csharp
public interface INotificationService
{
    void Send(string message);
}

public class EmailNotificationService : INotificationService { }
public class SmsNotificationService : INotificationService { }
public class PushNotificationService : INotificationService { }

// Registration
builder.RegisterType<EmailNotificationService>()
    .Keyed<INotificationService>("email");
builder.RegisterType<SmsNotificationService>()
    .Keyed<INotificationService>("sms");
builder.RegisterType<PushNotificationService>()
    .Keyed<INotificationService>("push");

// Usage
public class NotificationController : Controller
{
    private readonly IIndex<string, INotificationService> _notificationServices;
    
    public NotificationController(IIndex<string, INotificationService> notificationServices)
    {
        _notificationServices = notificationServices;
    }
    
    public ActionResult Send(string channel, string message)
    {
        if (_notificationServices.TryGetValue(channel, out var service))
        {
            service.Send(message);
            return Json(new { success = true });
        }
        
        return Json(new { error = "Unknown channel" });
    }
}
```

##### 4. Factory with Parameters

```csharp
public delegate IReportGenerator ReportGeneratorFactory(ReportType type);

// Registration
builder.RegisterType<PdfReportGenerator>()
    .Keyed<IReportGenerator>(ReportType.Pdf);
builder.RegisterType<ExcelReportGenerator>()
    .Keyed<IReportGenerator>(ReportType.Excel);

builder.Register<ReportGeneratorFactory>(c =>
{
    var context = c.Resolve<IComponentContext>();
    return type => context.ResolveKeyed<IReportGenerator>(type);
});

// Usage
public class ReportController : Controller
{
    private readonly ReportGeneratorFactory _reportFactory;
    
    public ReportController(ReportGeneratorFactory reportFactory)
    {
        _reportFactory = reportFactory;
    }
    
    public ActionResult Generate(ReportType type, ReportData data)
    {
        var generator = _reportFactory(type);
        var report = generator.Generate(data);
        return File(report.Content, report.MimeType, report.FileName);
    }
}
```

#### Web Forms with Factories

```csharp
public partial class OrderPage : System.Web.UI.Page
{
    // Property injection with factories
    public Func<IOrderService> OrderServiceFactory { get; set; }
    public Func<IEmailService> EmailServiceFactory { get; set; }
    public Lazy<IReportService> ReportService { get; set; }
    
    protected void Page_Load(object sender, EventArgs e)
    {
        if (!IsPostBack)
        {
            // Only instantiate order service
            var orderService = OrderServiceFactory();
            var orders = orderService.GetRecentOrders();
            OrderGrid.DataSource = orders;
            OrderGrid.DataBind();
        }
    }
    
    protected void btnSendEmail_Click(object sender, EventArgs e)
    {
        // Only instantiate when button clicked
        var emailService = EmailServiceFactory();
        var orderService = OrderServiceFactory();
        
        var order = orderService.GetOrder(int.Parse(hdnOrderId.Value));
        emailService.SendOrderUpdate(order);
    }
    
    protected void btnGenerateReport_Click(object sender, EventArgs e)
    {
        // Lazy - instantiated on first access
        var report = ReportService.Value.GenerateOrderReport();
        // ... handle report
    }
}
```

#### Registration Patterns for Factories

```csharp
public static class AutofacConfig
{
    public static void RegisterDependencies()
    {
        var builder = new ContainerBuilder();
        
        // Services automatically support Func<T>, Lazy<T>, Owned<T>
        builder.RegisterType<OrderService>()
            .As<IOrderService>()
            .InstancePerRequest();
        
        // No special registration needed for factories!
        // Autofac automatically provides:
        // - Func<IOrderService>
        // - Lazy<IOrderService>
        // - Owned<IOrderService>
        // - Func<Owned<IOrderService>>
        // - Lazy<Func<IOrderService>>
        // etc.
        
        // For complex scenarios, register factory explicitly
        builder.Register(c =>
        {
            var context = c.Resolve<IComponentContext>();
            Func<OrderType, IOrderProcessor> factory = orderType =>
            {
                switch (orderType)
                {
                    case OrderType.Standard:
                        return context.Resolve<StandardOrderProcessor>();
                    case OrderType.Express:
                        return context.Resolve<ExpressOrderProcessor>();
                    case OrderType.Bulk:
                        return context.Resolve<BulkOrderProcessor>();
                    default:
                        throw new NotSupportedException($"Order type {orderType} not supported");
                }
            };
            return factory;
        }).As<Func<OrderType, IOrderProcessor>>()
          .InstancePerLifetimeScope();
        
        var container = builder.Build();
    }
}
```

### Scenario 5: SignalR Hubs with DI

```csharp
public class ChatHub : Hub
{
    private readonly IMessageService _messageService;
    private readonly IUserService _userService;
    private readonly ILogger _logger;
    
    public ChatHub(
        IMessageService messageService,
        IUserService userService,
        ILogger logger)
    {
        _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
        _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task SendMessage(string message)
    {
        try
        {
            var user = _userService.GetUser(Context.UserIdentifier);
            var savedMessage = await _messageService.SaveMessageAsync(user.Id, message);
            
            Clients.All.broadcastMessage(user.Name, savedMessage.Content, savedMessage.Timestamp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message");
            Clients.Caller.showError("Failed to send message");
        }
    }
}

// SignalR configuration with Autofac
public class Startup
{
    public void Configuration(IAppBuilder app)
    {
        var container = AutofacConfig.Container;
        
        // Set SignalR dependency resolver
        var config = new HubConfiguration
        {
            Resolver = new AutofacDependencyResolver(container)
        };
        
        app.MapSignalR(config);
    }
}
```

## Factory Pattern Decision Matrix

The agent should automatically detect and apply Autofac factory patterns based on these criteria:

### When to Use Factory Patterns

| Scenario | Pattern to Use | Reason |
|----------|---------------|---------|
| Controller has >5 dependencies | `Func<T>` | Avoid instantiating unused services |
| Service might not be used | `Func<T>` | Deferred instantiation |
| Service used multiple times in method | `Lazy<T>` | Create once, reuse |
| Service needs explicit disposal | `Owned<T>` | Control lifetime scope |
| Multiple implementations of interface | `IIndex<K,V>` | Runtime selection |
| Service creation needs parameters | Custom Factory Delegate | Parameterized creation |
| Heavy initialization cost | `Lazy<T>` | Defer until actually needed |
| Service used in <30% of methods | `Func<T>` | Most methods don't need it |

### Detection Algorithm

```python
def should_use_factory_pattern(class_info, dependency_info):
    """
    Determine if a class should use factory patterns for its dependencies
    """
    
    # Count total dependencies
    total_deps = len(dependency_info.dependencies)
    
    # Analyze usage patterns
    usage_analysis = analyze_dependency_usage(class_info, dependency_info)
    
    recommendations = []
    
    for dep in dependency_info.dependencies:
        # Rule 1: Rarely used dependencies
        if usage_analysis[dep].usage_percentage < 30:
            recommendations.append({
                "dependency": dep,
                "pattern": "Func<T>",
                "reason": f"Only used in {usage_analysis[dep].usage_percentage}% of methods"
            })
        
        # Rule 2: Heavy services
        elif is_heavy_service(dep):
            recommendations.append({
                "dependency": dep,
                "pattern": "Lazy<T>",
                "reason": "Heavy initialization cost"
            })
        
        # Rule 3: Disposable resources
        elif implements_idisposable(dep):
            recommendations.append({
                "dependency": dep,
                "pattern": "Owned<T>",
                "reason": "Needs controlled disposal"
            })
    
    # Rule 4: Too many dependencies
    if total_deps > 5:
        recommendations.append({
            "pattern": "ServiceAggregator",
            "reason": f"Class has {total_deps} dependencies - use aggregator pattern"
        })
    
    return recommendations

def is_heavy_service(dependency):
    """
    Identify services with heavy initialization
    """
    heavy_indicators = [
        "Report", "Export", "Import", "Pdf", "Excel",
        "Email", "Sms", "Analytics", "BigData",
        "Machine", "Learning", "AI", "Process"
    ]
    
    return any(indicator in dependency.name for indicator in heavy_indicators)
```

### Automatic Refactoring to Factories

```python
def refactor_to_factory_pattern(controller, recommendations):
    """
    Automatically refactor a controller to use factory patterns
    """
    
    if any(r["pattern"] == "ServiceAggregator" for r in recommendations):
        return create_service_aggregator(controller, recommendations)
    
    # Replace individual dependencies
    for rec in recommendations:
        if rec["pattern"] == "Func<T>":
            replace_with_func_factory(controller, rec["dependency"])
        elif rec["pattern"] == "Lazy<T>":
            replace_with_lazy_factory(controller, rec["dependency"])
        elif rec["pattern"] == "Owned<T>":
            replace_with_owned_factory(controller, rec["dependency"])

def create_service_aggregator(controller, recommendations):
    """
    Create a service aggregator for controllers with many dependencies
    """
    
    # Generate aggregator interface
    interface_code = f"""
    public interface I{controller.name}Services
    {{
        {generate_factory_properties(controller.dependencies)}
    }}
    """
    
    # Generate aggregator implementation
    implementation_code = f"""
    public class {controller.name}Services : I{controller.name}Services
    {{
        public {controller.name}Services(
            {generate_factory_parameters(controller.dependencies)})
        {{
            {generate_property_assignments(controller.dependencies)}
        }}
        
        {generate_properties(controller.dependencies)}
    }}
    """
    
    # Update controller to use aggregator
    update_controller_to_use_aggregator(controller, interface_name)
```

## Agent Workflow Implementation

```python
async def implement_dependency_injection(project_path):
    """
    Complete workflow for implementing DI in a .NET Framework 4.7.2 project
    """
    
    # Phase 1: Analysis
    print("🔍 Analyzing project structure...")
    project_info = analyze_project_structure()
    
    # Phase 2: Infrastructure Setup
    print("🏗️ Setting up DI infrastructure...")
    
    # Step 1: Install packages
    install_nuget_packages(project_info)
    
    # Step 2: Create configuration
    create_di_configuration(project_info)
    
    # Step 3: Update Global.asax
    update_global_asax(project_info)
    
    # Step 4: Update Web.config
    update_web_config(project_info)
    
    # Phase 3: Service Migration
    print("🔄 Migrating services to use DI...")
    
    # Find all service classes
    services = find_service_classes()
    for service in services:
        # Extract interface
        interface = extract_interface(service)
        
        # Convert to DI pattern
        convert_service_to_di(service, interface)
        
        # Register in container
        register_service(service, interface)
    
    # Phase 4: Controller/Page Migration
    print("📝 Migrating controllers and pages...")
    
    if "MVC5" in project_info["types"]:
        migrate_mvc_controllers()
    
    if "WebForms" in project_info["types"]:
        migrate_webforms_pages()
    
    if "WebAPI2" in project_info["types"]:
        migrate_webapi_controllers()
    
    # Phase 5: Verification
    print("✅ Verifying implementation...")
    verify_di_implementation()
    
    print("🎉 Dependency injection implementation complete!")

def find_service_classes():
    """Find all classes that should be converted to services"""
    
    # Find classes with data access
    data_classes = spelunk-find-statements(
        pattern="new SqlConnection|new DbContext|new HttpClient",
        patternType="text"
    )
    
    # Find classes with external dependencies
    dependency_classes = spelunk-find-statements(
        pattern="ConfigurationManager|HttpContext.Current|File.Read|File.Write",
        patternType="text"
    )
    
    # Find repository and service classes
    service_classes = spelunk-find-class(
        pattern="*Service|*Repository|*Provider|*Manager"
    )
    
    return merge_unique(data_classes, dependency_classes, service_classes)

def extract_interface(class_info):
    """Extract interface from existing class"""
    
    # Get public methods
    methods = spelunk-find-method(
        classPattern=class_info.name,
        methodPattern="*"
    )
    
    # Generate interface
    interface_code = f"""
    public interface I{class_info.name}
    {{
        {generate_interface_methods(methods)}
    }}
    """
    
    # Add interface file
    spelunk-edit-code(
        operation="add-interface",
        code=interface_code
    )
    
    return f"I{class_info.name}"

def convert_service_to_di(service, interface):
    """Convert service to use constructor injection"""
    
    # Find dependencies
    dependencies = analyze_dependencies(service)
    
    # Add constructor
    constructor_code = generate_constructor(dependencies)
    spelunk-edit-code(
        operation="add-constructor",
        className=service.name,
        code=constructor_code
    )
    
    # Replace direct instantiation
    for dep in dependencies:
        replace_instantiation_with_field(service, dep)
    
    # Implement interface
    spelunk-edit-code(
        operation="implement-interface",
        className=service.name,
        interface=interface
    )

def migrate_mvc_controllers():
    """Migrate MVC controllers to use DI"""
    
    controllers = spelunk-find-class(pattern="*Controller")
    
    for controller in controllers:
        # Find dependencies
        deps = find_controller_dependencies(controller)
        
        # Add constructor
        if deps:
            add_constructor_injection(controller, deps)
        
        # Remove direct instantiation
        remove_direct_instantiation(controller)
        
        # Update action methods
        update_action_methods(controller)

def migrate_webforms_pages():
    """Migrate Web Forms pages to use DI"""
    
    pages = spelunk-find-class(pattern="*Page")
    
    for page in pages:
        # Find dependencies
        deps = find_page_dependencies(page)
        
        # Add properties for injection
        if deps:
            add_property_injection(page, deps)
        
        # Update Page_Load and event handlers
        update_page_methods(page)
        
        # Handle async operations
        if has_async_operations(page):
            convert_to_registerasynctask(page)
```

## Testing Strategy

### Unit Testing with DI

```csharp
[TestClass]
public class UserServiceTests
{
    private Mock<IUserRepository> _mockRepository;
    private Mock<ICacheService> _mockCache;
    private Mock<ILogger> _mockLogger;
    private UserService _service;
    
    [TestInitialize]
    public void Setup()
    {
        _mockRepository = new Mock<IUserRepository>();
        _mockCache = new Mock<ICacheService>();
        _mockLogger = new Mock<ILogger>();
        
        _service = new UserService(
            _mockRepository.Object,
            _mockCache.Object,
            _mockLogger.Object);
    }
    
    [TestMethod]
    public void GetAllUsers_WhenCached_ReturnsFromCache()
    {
        // Arrange
        var cachedUsers = new List<User> { new User { Id = 1, Name = "Test" } };
        _mockCache.Setup(c => c.Get<List<User>>("all_users"))
            .Returns(cachedUsers);
        
        // Act
        var result = _service.GetAllUsers();
        
        // Assert
        Assert.AreEqual(cachedUsers, result);
        _mockRepository.Verify(r => r.GetAll(), Times.Never);
    }
    
    [TestMethod]
    public void GetAllUsers_WhenNotCached_ReturnsFromRepository()
    {
        // Arrange
        var users = new List<User> { new User { Id = 1, Name = "Test" } };
        _mockCache.Setup(c => c.Get<List<User>>("all_users"))
            .Returns((List<User>)null);
        _mockRepository.Setup(r => r.GetAll())
            .Returns(users);
        
        // Act
        var result = _service.GetAllUsers();
        
        // Assert
        Assert.AreEqual(users, result);
        _mockCache.Verify(c => c.Set("all_users", users, It.IsAny<TimeSpan>()), Times.Once);
    }
}
```

### Integration Testing

```csharp
[TestClass]
public class DependencyInjectionIntegrationTests
{
    private IContainer _container;
    
    [TestInitialize]
    public void Setup()
    {
        var builder = new ContainerBuilder();
        
        // Register test doubles
        builder.RegisterType<TestDbConnection>()
            .As<IDbConnection>()
            .InstancePerLifetimeScope();
        
        // Register real services
        builder.RegisterType<UserRepository>()
            .As<IUserRepository>();
        
        builder.RegisterType<UserService>()
            .As<IUserService>();
        
        _container = builder.Build();
    }
    
    [TestMethod]
    public void Container_ResolvesAllDependencies()
    {
        using (var scope = _container.BeginLifetimeScope())
        {
            // Should resolve without throwing
            var service = scope.Resolve<IUserService>();
            Assert.IsNotNull(service);
        }
    }
    
    [TestMethod]
    public void WebFormsPage_ReceivesInjectedDependencies()
    {
        using (var scope = _container.BeginLifetimeScope())
        {
            var page = new UserListPage();
            
            // Simulate property injection
            var properties = _container.ComponentRegistry
                .TryGetRegistration(new TypedService(typeof(IUserService)), out var registration);
            
            if (registration != null)
            {
                page.UserService = scope.Resolve<IUserService>();
            }
            
            Assert.IsNotNull(page.UserService);
        }
    }
}
```

## Common Issues and Solutions

### Issue 1: Circular Dependencies
```csharp
// Problem: A depends on B, B depends on A
// Solution: Use factory pattern or lazy initialization

builder.Register(c => new Lazy<IServiceA>(() => c.Resolve<IServiceA>()))
    .As<Lazy<IServiceA>>()
    .InstancePerRequest();
```

### Issue 2: Web Forms Designer Issues
```csharp
// Problem: Designer doesn't work with DI
// Solution: Add null checks and default values

public partial class MyPage : Page
{
    public IUserService UserService { get; set; }
    
    protected void Page_Load(object sender, EventArgs e)
    {
        // Null check for designer
        if (UserService == null && DesignMode)
        {
            return;
        }
        
        // Normal code
        var users = UserService?.GetAllUsers() ?? new List<User>();
    }
}
```

### Issue 3: Session/Cache in Constructor
```csharp
// Problem: HttpContext not available in constructor
// Solution: Use factory or property injection

// Wrong
public class BadService
{
    private readonly string _userId;
    
    public BadService()
    {
        _userId = HttpContext.Current.Session["UserId"]; // Null!
    }
}

// Right
public class GoodService
{
    private readonly IHttpContextAccessor _contextAccessor;
    
    public GoodService(IHttpContextAccessor contextAccessor)
    {
        _contextAccessor = contextAccessor;
    }
    
    private string UserId => _contextAccessor.Session["UserId"]?.ToString();
}
```

## Success Criteria

The DI implementation is successful when:

1. ✅ All controllers use constructor injection
2. ✅ All pages/user controls use property injection
3. ✅ No direct instantiation of services
4. ✅ All services have interfaces
5. ✅ Database connections are properly managed
6. ✅ HttpContext dependencies are abstracted
7. ✅ Unit tests can mock all dependencies
8. ✅ Container configuration is centralized
9. ✅ Per-request scope is properly configured
10. ✅ No memory leaks from singleton misuse

## VB.NET Examples

### VB.NET MVC Controller
```vb
' Before
Public Class UserController
    Inherits Controller
    
    Public Function Index() As ActionResult
        Dim dbContext = New ApplicationDbContext()
        Dim userService = New UserService(dbContext)
        Dim users = userService.GetAllUsers()
        Return View(users)
    End Function
End Class

' After
Public Class UserController
    Inherits Controller
    
    Private ReadOnly _userService As IUserService
    Private ReadOnly _logger As ILogger
    
    Public Sub New(userService As IUserService, logger As ILogger)
        _userService = If(userService, Throw New ArgumentNullException(NameOf(userService)))
        _logger = If(logger, Throw New ArgumentNullException(NameOf(logger)))
    End Sub
    
    Public Function Index() As ActionResult
        Try
            Dim users = _userService.GetAllUsers()
            Return View(users)
        Catch ex As Exception
            _logger.LogError(ex, "Failed to load users")
            Throw
        End Try
    End Function
End Class
```

### VB.NET Web Forms Page
```vb
' Before
Public Partial Class UserList
    Inherits System.Web.UI.Page
    
    Protected Sub Page_Load(sender As Object, e As EventArgs)
        If Not IsPostBack Then
            Using dbContext = New ApplicationDbContext()
                Dim userService = New UserService(dbContext)
                Dim users = userService.GetAllUsers()
                
                UserGrid.DataSource = users
                UserGrid.DataBind()
            End Using
        End If
    End Sub
End Class

' After
Public Partial Class UserList
    Inherits System.Web.UI.Page
    
    Public Property UserService As IUserService
    Public Property Logger As ILogger
    
    Protected Sub Page_Load(sender As Object, e As EventArgs)
        If Not IsPostBack Then
            Try
                Dim users = UserService.GetAllUsers()
                
                UserGrid.DataSource = users
                UserGrid.DataBind()
            Catch ex As Exception
                Logger.LogError("Failed to load users", ex)
                ShowError("Unable to load users. Please try again.")
            End Try
        End If
    End Sub
End Class
```

## Conclusion

This agent transforms legacy .NET Framework 4.7.2 applications to use modern dependency injection patterns, improving:
- **Testability** - All dependencies can be mocked
- **Maintainability** - Clear separation of concerns
- **Flexibility** - Easy to swap implementations
- **Reliability** - Proper resource management

The migration can be done incrementally, starting with the most critical services and gradually expanding to cover the entire application.