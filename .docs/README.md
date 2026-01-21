# HttpProxy Library

## Overview

**HttpProxy** is a modern .NET library that provides a fluent, developer-friendly wrapper around HTTP client communication. Built on top of **Refit** and **Polly**, it simplifies the configuration of resilient HTTP clients with minimal boilerplate code.

### Key Benefits

- **Fluent API Design**: Chainable methods for intuitive configuration
- **Built-in Resilience**: Automatic retry, circuit breaker, and timeout policies
- **Authentication Made Easy**: Built-in support for JWT Bearer and Basic Auth
- **Type-Safe**: Leverages Refit for compile-time API contract validation
- **Production-Ready**: Thread-safe, well-tested, and battle-hardened
- **DI-Friendly**: First-class support for dependency injection

### When to Use HttpProxy

- Building microservices that communicate via HTTP
- Consuming third-party REST APIs
- Implementing resilient API clients with retry logic
- Managing authentication across multiple API calls
- Creating robust HTTP communication with circuit breaker patterns

---

## Target Framework

- **.NET 8.0+**

---

## Dependencies

HttpProxy leverages these powerful libraries:

- **Refit** (9.0.2) - Type-safe REST library
- **Polly** (8.6.5) - Resilience and transient-fault-handling
- **Microsoft.Extensions.Http** (8.0.1) - HttpClient factory integration
- **Microsoft.Extensions.DependencyInjection.Abstractions** (8.0.2) - DI support

---

## Installation

### Via NuGet Package Manager

```bash
Install-Package HttpProxy
```

### Via .NET CLI

```bash
dotnet add package HttpProxy
```

### Via PackageReference

Add to your `.csproj` file:

```xml
<PackageReference Include="HttpProxy" Version="1.0.0" />
```

---

## Quick Start

### 1. Define Your API Interface (Refit)

```csharp
using Refit;
using HttpProxy.ProxyAttribute;

[ProxyBaseUri("ApiSettings:JsonPlaceholder:BaseUrl")]
public interface IJsonPlaceholderApi
{
    [Get("/users")]
    Task<List<User>> GetUsersAsync();

    [Get("/users/{id}")]
    Task<User> GetUserByIdAsync(int id);

    [Post("/users")]
    Task<User> CreateUserAsync([Body] User user);
}
```

### 2. Configure Base URL in appsettings.json

```json
{
  "ApiSettings": {
    "JsonPlaceholder": {
      "BaseUrl": "https://jsonplaceholder.typicode.com"
    }
  }
}
```

### 3. Create and Use the Proxy

```csharp
using HttpProxy.Interface;
using HttpProxy.Proxy;

// Inject dependencies (IConfiguration, IHttpClientFactory, ILogger)
var proxyFabric = new ProxyFabric<IJsonPlaceholderApi>(
    configuration,
    httpClientFactory,
    logger
);

// Get the configured proxy
var api = proxyFabric.Proxy;

// Make API calls
var users = await api.GetUsersAsync();
var user = await api.GetUserByIdAsync(1);
```

---

## Usage Examples

### Example 1: Basic HTTP Client Setup

```csharp
using HttpProxy.Interface;
using HttpProxy.Proxy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class ApiService
{
    private readonly IJsonPlaceholderApi _api;

    public ApiService(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<ProxyFabric<IJsonPlaceholderApi>> logger)
    {
        // Create proxy fabric
        var proxyFabric = new ProxyFabric<IJsonPlaceholderApi>(
            configuration,
            httpClientFactory,
            logger
        );

        // Get the API proxy
        _api = proxyFabric.Proxy;
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        return await _api.GetUsersAsync();
    }
}
```

---

### Example 2: Adding Custom Headers

```csharp
var headers = new List<KeyValuePair<string, string>>
{
    new("X-Api-Key", "your-secret-api-key"),
    new("X-Correlation-Id", Guid.NewGuid().ToString()),
    new("X-Client-Version", "1.0.0")
};

var proxyFabric = new ProxyFabric<IMyApi>(configuration, httpClientFactory, logger)
    .WithHeaders(headers);

var api = proxyFabric.Proxy;
var result = await api.GetDataAsync();
```

**Note:** Headers are stored in a thread-safe `ConcurrentDictionary` and applied to all subsequent requests.

---

### Example 3: JWT Bearer Authentication

```csharp
// Obtain your JWT token (from login, identity provider, etc.)
string jwtToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...";

var proxyFabric = new ProxyFabric<ISecureApi>(configuration, httpClientFactory, logger)
    .WithAuthorization(jwtToken);

var api = proxyFabric.Proxy;
var secureData = await api.GetSecureDataAsync();
```

**Under the hood:** Sets the `Authorization: Bearer <token>` header automatically.

---

### Example 4: HTTP Basic Authentication

```csharp
var proxyFabric = new ProxyFabric<IBasicAuthApi>(configuration, httpClientFactory, logger)
    .WithBasicAuth("username", "password");

var api = proxyFabric.Proxy;
var data = await api.GetProtectedResourceAsync();
```

**Under the hood:** Encodes credentials as Base64 and sets `Authorization: Basic <encoded>` header.

---

### Example 5: Retry Policy

Automatically retry failed requests with configurable delay and exponential backoff.

```csharp
var proxyFabric = new ProxyFabric<IUnreliableApi>(configuration, httpClientFactory, logger)
    .WithRetryPolicy(
        retryCount: 5,              // Retry up to 5 times
        delaySeconds: 2,            // Wait 2 seconds between retries
        exponentialBackoff: true    // Use exponential backoff (2s, 4s, 8s, 16s, 32s)
    );

var api = proxyFabric.Proxy;

// If the API returns 5xx errors or throws exceptions,
// it will automatically retry up to 5 times
var data = await api.GetDataAsync();
```

**Retry Conditions:**
- 5xx server errors
- 408 Request Timeout
- 503 Service Unavailable
- `HttpRequestException`
- `TaskCanceledException`
- `TimeoutException`

**Exponential Backoff Formula:**
```
Delay = 2^(attempt - 1) × delaySeconds
```

---

### Example 6: Circuit Breaker Policy

Prevent cascading failures by "opening the circuit" after consecutive failures.

```csharp
var proxyFabric = new ProxyFabric<IExternalApi>(configuration, httpClientFactory, logger)
    .WithCircuitBreaker(
        failureThreshold: 5,        // Open circuit after 5 consecutive failures
        durationOfBreakSeconds: 30  // Keep circuit open for 30 seconds
    );

var api = proxyFabric.Proxy;
var data = await api.GetDataAsync();
```

**Circuit States:**
1. **Closed** 🟢 - Normal operation, requests flow through
2. **Open** 🔴 - Circuit is broken, requests fail immediately without calling the API
3. **Half-Open** 🟡 - Testing if the service has recovered

**Logged Events:**
- Circuit opens: `🔴 Circuit breaker opened for {duration} seconds`
- Circuit resets: `🟢 Circuit breaker reset to closed state`
- Half-open state: `🟡 Circuit breaker is in half-open state, testing`

---

### Example 7: Timeout Policy

Set a maximum duration for API requests.

```csharp
var proxyFabric = new ProxyFabric<ISlowApi>(configuration, httpClientFactory, logger)
    .WithTimeout(30); // 30-second timeout

var api = proxyFabric.Proxy;

try
{
    var data = await api.GetSlowDataAsync();
}
catch (TimeoutException ex)
{
    // Handle timeout
    Console.WriteLine("Request timed out after 30 seconds");
}
```

**Default:** 30 seconds if not specified.

---

### Example 8: Combining Multiple Policies

Create a robust, production-ready HTTP client with all resilience features enabled.

```csharp
var proxyFabric = new ProxyFabric<IProductionApi>(configuration, httpClientFactory, logger)
    .WithRetryPolicy(retryCount: 3, delaySeconds: 2, exponentialBackoff: true)
    .WithCircuitBreaker(failureThreshold: 5, durationOfBreakSeconds: 60)
    .WithTimeout(30);

var api = proxyFabric.Proxy;
var data = await api.GetDataAsync();
```

**Policy Execution Order** (nested):

```
Circuit Breaker (outermost)
    ↓
Retry Policy
    ↓
Timeout Policy (innermost)
    ↓
HTTP Request
```

**What Happens:**
1. Circuit breaker checks if circuit is open
2. If closed, retry policy wraps the request
3. Each retry attempt has a 30-second timeout
4. If all retries fail, circuit breaker counts the failure
5. After 5 consecutive failures, circuit opens for 60 seconds

---

### Example 9: Complete Real-World Scenario

A production-ready API client with authentication, custom headers, and full resilience.

```csharp
using HttpProxy.Interface;
using HttpProxy.Proxy;
using Refit;

// 1. Define the API interface
[ProxyBaseUri("ApiSettings:PaymentGateway:BaseUrl")]
public interface IPaymentGatewayApi
{
    [Post("/transactions")]
    Task<TransactionResponse> ProcessPaymentAsync([Body] PaymentRequest payment);

    [Get("/transactions/{transactionId}")]
    Task<TransactionResponse> GetTransactionAsync(string transactionId);
}

// 2. Create a service class
public class PaymentService
{
    private readonly IPaymentGatewayApi _api;

    public PaymentService(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<ProxyFabric<IPaymentGatewayApi>> logger,
        string apiKey,
        string jwtToken)
    {
        // Custom headers for API key and correlation tracking
        var headers = new List<KeyValuePair<string, string>>
        {
            new("X-Api-Key", apiKey),
            new("X-Correlation-Id", Guid.NewGuid().ToString()),
            new("X-Client-Version", "2.0.0"),
            new("X-Request-Source", "MyApp")
        };

        // Build a robust proxy with all resilience features
        var proxyFabric = new ProxyFabric<IPaymentGatewayApi>(
                configuration,
                httpClientFactory,
                logger
            )
            .WithHeaders(headers)
            .WithAuthorization(jwtToken)
            .WithRetryPolicy(
                retryCount: 3,
                delaySeconds: 1,
                exponentialBackoff: true
            )
            .WithCircuitBreaker(
                failureThreshold: 5,
                durationOfBreakSeconds: 60
            )
            .WithTimeout(45);

        _api = proxyFabric.Proxy;
    }

    public async Task<TransactionResponse> ProcessPaymentAsync(PaymentRequest payment)
    {
        try
        {
            return await _api.ProcessPaymentAsync(payment);
        }
        catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            // Handle validation errors
            throw new PaymentValidationException("Invalid payment data", ex);
        }
        catch (BrokenCircuitException ex)
        {
            // Circuit is open - service is unavailable
            throw new ServiceUnavailableException("Payment gateway is temporarily unavailable", ex);
        }
        catch (TimeoutException ex)
        {
            // Request timed out
            throw new PaymentTimeoutException("Payment processing timed out", ex);
        }
    }

    public async Task<TransactionResponse> GetTransactionStatusAsync(string transactionId)
    {
        return await _api.GetTransactionAsync(transactionId);
    }
}

// 3. Configuration (appsettings.json)
// {
//   "ApiSettings": {
//     "PaymentGateway": {
//       "BaseUrl": "https://api.paymentgateway.com/v1"
//     }
//   }
// }
```

---

### Example 10: Dependency Injection Setup

Integrate HttpProxy with ASP.NET Core's built-in DI container.

```csharp
using HttpProxy.IoC;
using Microsoft.Extensions.DependencyInjection;

// In Program.cs or Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    // Register HttpProxy configuration
    services.AddHttpProxyConfiguration();

    // Register your API proxies
    services.AddScoped<IProxyFabric<IJsonPlaceholderApi>>(provider =>
    {
        var configuration = provider.GetRequiredService<IConfiguration>();
        var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
        var logger = provider.GetRequiredService<ILogger<ProxyFabric<IJsonPlaceholderApi>>>();

        return new ProxyFabric<IJsonPlaceholderApi>(
            configuration,
            httpClientFactory,
            logger
        )
        .WithRetryPolicy(3, 2, true)
        .WithCircuitBreaker(5, 30)
        .WithTimeout(30);
    });

    // Register your service that uses the proxy
    services.AddScoped<IUserService, UserService>();
}
```

**Usage in Controller:**

```csharp
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IProxyFabric<IJsonPlaceholderApi> _proxyFabric;

    public UsersController(IProxyFabric<IJsonPlaceholderApi> proxyFabric)
    {
        _proxyFabric = proxyFabric;
    }

    [HttpGet]
    public async Task<ActionResult<List<User>>> GetUsers()
    {
        var api = _proxyFabric.Proxy;
        var users = await api.GetUsersAsync();
        return Ok(users);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<User>> GetUser(int id)
    {
        var api = _proxyFabric.Proxy;
        var user = await api.GetUserByIdAsync(id);
        return Ok(user);
    }
}
```

---

## API Reference

### IProxyFabric<T> Interface

All methods return `IProxyFabric<T>` for method chaining (fluent API).

#### `WithHeaders(List<KeyValuePair<string, string>> headers)`

Adds custom HTTP headers to all requests.

**Parameters:**
- `headers`: List of key-value pairs representing HTTP headers

**Returns:** `IProxyFabric<T>`

**Example:**
```csharp
.WithHeaders(new List<KeyValuePair<string, string>>
{
    new("X-Api-Key", "secret"),
    new("X-Custom-Header", "value")
})
```

---

#### `WithAuthorization(string token)`

Sets JWT Bearer token authentication.

**Parameters:**
- `token`: JWT token string (without "Bearer" prefix)

**Returns:** `IProxyFabric<T>`

**Example:**
```csharp
.WithAuthorization("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...")
```

**Header Set:** `Authorization: Bearer <token>`

---

#### `WithBasicAuth(string user, string password)`

Sets HTTP Basic authentication.

**Parameters:**
- `user`: Username
- `password`: Password

**Returns:** `IProxyFabric<T>`

**Example:**
```csharp
.WithBasicAuth("admin", "password123")
```

**Header Set:** `Authorization: Basic <base64(user:password)>`

---

#### `WithRetryPolicy(int retryCount = 3, int delaySeconds = 2, bool exponentialBackoff = true)`

Configures automatic retry logic for failed requests.

**Parameters:**
- `retryCount`: Number of retry attempts (default: 3)
- `delaySeconds`: Delay between retries in seconds (default: 2)
- `exponentialBackoff`: Use exponential backoff strategy (default: true)

**Returns:** `IProxyFabric<T>`

**Example:**
```csharp
.WithRetryPolicy(retryCount: 5, delaySeconds: 3, exponentialBackoff: true)
```

**Retry Conditions:**
- HTTP 5xx status codes
- HTTP 408 (Request Timeout)
- HTTP 503 (Service Unavailable)
- `HttpRequestException`
- `TaskCanceledException`
- `TimeoutException`

---

#### `WithCircuitBreaker(int failureThreshold = 5, int durationOfBreakSeconds = 30)`

Configures circuit breaker pattern to prevent cascading failures.

**Parameters:**
- `failureThreshold`: Number of consecutive failures before opening the circuit (default: 5)
- `durationOfBreakSeconds`: Duration to keep circuit open in seconds (default: 30)

**Returns:** `IProxyFabric<T>`

**Example:**
```csharp
.WithCircuitBreaker(failureThreshold: 10, durationOfBreakSeconds: 60)
```

**Circuit States:**
- **Closed**: Normal operation
- **Open**: Circuit broken, requests fail immediately
- **Half-Open**: Testing if service has recovered

---

#### `WithTimeout(int timeoutSeconds = 30)`

Sets request timeout duration.

**Parameters:**
- `timeoutSeconds`: Timeout in seconds (default: 30)

**Returns:** `IProxyFabric<T>`

**Example:**
```csharp
.WithTimeout(45)
```

**Exception:** Throws `TimeoutException` if request exceeds timeout.

---

#### `Proxy` Property

Gets the configured Refit proxy instance.

**Type:** `T` (your Refit interface)

**Example:**
```csharp
var api = proxyFabric.Proxy;
var data = await api.GetDataAsync();
```

---

## Configuration Details

### ProxyBaseUri Attribute

The `[ProxyBaseUri]` attribute specifies where to read the base URL from `IConfiguration`.

**Syntax:**
```csharp
[ProxyBaseUri("ConfigurationKey")]
public interface IMyApi
{
    // ...
}
```

**Example:**

```csharp
[ProxyBaseUri("ApiSettings:GitHub:BaseUrl")]
public interface IGitHubApi
{
    [Get("/users/{username}")]
    Task<GitHubUser> GetUserAsync(string username);
}
```

**appsettings.json:**
```json
{
  "ApiSettings": {
    "GitHub": {
      "BaseUrl": "https://api.github.com"
    }
  }
}
```

### IConfiguration Integration

HttpProxy reads the base URL from `IConfiguration` at runtime using the key specified in `[ProxyBaseUri]`.

**Validation:**
- Throws `InvalidOperationException` if attribute is missing
- Throws `ArgumentException` if configuration key is not found
- Throws `UriFormatException` if URL format is invalid

---

## Best Practices

### 1. When to Use Retry Policy

✅ **Use retry for:**
- Transient network failures
- Temporary service unavailability (503)
- Timeout errors on unreliable networks

❌ **Don't use retry for:**
- Client errors (4xx) - these won't fix themselves
- Authentication failures (401, 403)
- Resource not found (404)

---

### 2. When to Use Circuit Breaker

✅ **Use circuit breaker for:**
- Calling external third-party APIs
- Microservice-to-microservice communication
- Services with known reliability issues
- Preventing cascading failures

❌ **Don't use circuit breaker for:**
- Internal database calls (use connection pooling instead)
- File system operations
- In-memory operations

---

### 3. Combining Policies

**Recommended combination for production:**

```csharp
.WithRetryPolicy(3, 2, true)          // Quick retries for transient failures
.WithCircuitBreaker(5, 60)            // Prevent cascading failures
.WithTimeout(30)                      // Prevent hanging requests
```

**Policy execution order:** Circuit Breaker → Retry → Timeout

---

### 4. Error Handling

Always wrap API calls in try-catch blocks:

```csharp
try
{
    var data = await api.GetDataAsync();
}
catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
{
    // Handle 404
}
catch (BrokenCircuitException ex)
{
    // Circuit is open - service unavailable
}
catch (TimeoutException ex)
{
    // Request timed out
}
catch (HttpRequestException ex)
{
    // General HTTP error
}
```

---

### 5. Thread Safety

HttpProxy is fully thread-safe:

- Headers are stored in `ConcurrentDictionary`
- `HttpClient` is thread-safe by design
- Refit proxies are thread-safe
- Polly policies are thread-safe

**Safe for:**
- Concurrent requests in web applications
- Parallel task execution
- Multi-threaded environments

---

### 6. HttpClient Lifecycle

HttpProxy uses `IHttpClientFactory`, which manages `HttpClient` lifecycle properly:

- Avoids socket exhaustion
- Handles DNS changes correctly
- Prevents `ObjectDisposedException`

**Do NOT:**
- Create new `ProxyFabric` instances per request (use DI)
- Dispose `HttpClient` manually (factory handles it)

---

### 7. Logging

HttpProxy logs important events:

**Information Level:**
- Policy configuration changes
- Circuit breaker state transitions
- Retry attempts

**Debug Level:**
- Header additions
- Authentication configuration

**Warning Level:**
- Invalid inputs
- Timeout exceeded

**Ensure your logging is configured:**

```csharp
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.AddDebug();
    builder.SetMinimumLevel(LogLevel.Information);
});
```

---

## Advanced Topics

### Policy Execution Order

Polly policies are nested in this order:

```
1. Circuit Breaker (outermost)
   ↓
2. Retry Policy
   ↓
3. Timeout Policy (innermost)
   ↓
HTTP Request
```

**Example flow:**
1. Circuit breaker checks if open/closed
2. If closed, retry policy executes
3. Each retry has its own timeout
4. If timeout occurs, retry catches it and retries
5. If all retries fail, circuit breaker counts failure

---

### Custom Headers Update

Headers can be updated multiple times:

```csharp
var proxyFabric = new ProxyFabric<IMyApi>(...)
    .WithHeaders(new List<KeyValuePair<string, string>>
    {
        new("X-Version", "1.0")
    });

// Later, add more headers
proxyFabric.WithHeaders(new List<KeyValuePair<string, string>>
{
    new("X-Request-Id", Guid.NewGuid().ToString())
});

// Both headers are now present
```

**Note:** If a header key already exists, its value is updated.

---

### Exponential Backoff Calculation

When `exponentialBackoff: true`, delays are calculated as:

```
Delay = 2^(attempt - 1) × delaySeconds
```

**Example with `delaySeconds: 2`:**
- Attempt 1: 2^0 × 2 = 1 × 2 = **2 seconds**
- Attempt 2: 2^1 × 2 = 2 × 2 = **4 seconds**
- Attempt 3: 2^2 × 2 = 4 × 2 = **8 seconds**
- Attempt 4: 2^3 × 2 = 8 × 2 = **16 seconds**
- Attempt 5: 2^4 × 2 = 16 × 2 = **32 seconds**

---

### Retry vs Circuit Breaker

**Retry Policy:**
- Immediate recovery mechanism
- Retries individual requests
- Good for transient failures
- Can increase load on failing service

**Circuit Breaker:**
- Preventive mechanism
- Stops all requests when threshold met
- Good for sustained failures
- Reduces load on failing service
- Allows service time to recover

**Use both together** for optimal resilience:
- Retry handles brief glitches
- Circuit breaker prevents cascading failures

---

### No-Op Policy

If no policies are configured, HttpProxy uses `Policy.NoOpAsync()`:

```csharp
var proxyFabric = new ProxyFabric<IMyApi>(configuration, httpClientFactory, logger);
// No policies configured - requests execute without Polly wrapper
```

This is fine for:
- Internal APIs with high reliability
- Local development
- APIs behind a resilient gateway (e.g., Azure API Management)

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                      Your Application                        │
│                                                              │
│  ┌────────────────────────────────────────────────────┐    │
│  │         IProxyFabric<TInterface>                   │    │
│  │  (Fluent Configuration API)                        │    │
│  └─────────────────┬──────────────────────────────────┘    │
│                    │                                         │
│  ┌─────────────────▼──────────────────────────────────┐    │
│  │         ProxyFabric<TInterface>                     │    │
│  │                                                     │    │
│  │  • Manages HttpClient                              │    │
│  │  • Configures Polly Policies                       │    │
│  │  • Creates Refit Proxy                             │    │
│  └──┬───────────┬────────────┬────────────────────────┘    │
│     │           │            │                              │
│     │           │            │                              │
│  ┌──▼────┐  ┌──▼─────┐  ┌───▼──────────────┐              │
│  │Config │  │HttpCli-│  │ Polly Policies   │              │
│  │ura-   │  │ent     │  │                  │              │
│  │tion   │  │Factory │  │ • Retry          │              │
│  └───────┘  └────────┘  │ • Circuit Breaker│              │
│                         │ • Timeout         │              │
│                         └──────┬────────────┘              │
│                                │                            │
│                    ┌───────────▼────────────┐              │
│                    │  Refit Proxy (T)       │              │
│                    │  (Type-safe API)       │              │
│                    └───────────┬────────────┘              │
└────────────────────────────────┼─────────────────────────┘
                                 │
                        ┌────────▼──────────┐
                        │   HTTP Request    │
                        │   to External API │
                        └───────────────────┘
```

---

## Troubleshooting

### Issue: "ProxyBaseUri attribute not found"

**Cause:** Missing `[ProxyBaseUri]` attribute on interface.

**Solution:**
```csharp
[ProxyBaseUri("YourConfigKey")]
public interface IYourApi { ... }
```

---

### Issue: "Configuration key not found"

**Cause:** Configuration key doesn't exist in `appsettings.json`.

**Solution:**
```json
{
  "YourConfigKey": "https://api.example.com"
}
```

---

### Issue: Timeout not working

**Cause:** HttpClient timeout conflicts with Polly timeout.

**Solution:** HttpProxy automatically sets `HttpClient.Timeout` to `Timeout.InfiniteTimeSpan`. Use `.WithTimeout()` instead.

---

### Issue: Too many retries

**Cause:** Default retry count might be too high for your use case.

**Solution:**
```csharp
.WithRetryPolicy(retryCount: 1, delaySeconds: 1, exponentialBackoff: false)
```

---

### Issue: Circuit breaker opens too quickly

**Cause:** Failure threshold is too low.

**Solution:**
```csharp
.WithCircuitBreaker(failureThreshold: 10, durationOfBreakSeconds: 60)
```

---

## Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch
3. Write tests for new functionality
4. Ensure all tests pass
5. Submit a pull request

---

## License

[Specify your license here]

---

## Support

For issues, questions, or feature requests, please open an issue on GitHub.

---

## Version History

### 1.0.0
- Initial release
- Fluent API for HTTP client configuration
- Retry, Circuit Breaker, and Timeout policies
- JWT Bearer and Basic Auth support
- Custom headers management
- Dependency injection integration

---

## Related Projects

- **Refit**: [https://github.com/reactiveui/refit](https://github.com/reactiveui/refit)
- **Polly**: [https://github.com/App-vNext/Polly](https://github.com/App-vNext/Polly)

---

**Happy coding!** 🚀
