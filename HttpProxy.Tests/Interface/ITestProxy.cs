using HttpProxy.Tests.ProxyFabric;
using HttpProxy.ProxyAttribute;
using Refit;

namespace HttpProxy.Tests.Interface;

[ProxyBaseUri("TEST_URL")]
public interface ITestProxy
{
    [Get("/test")]
    Task<HttpResponseMessage> GetTestAsync();

    [Get("/users")]
    Task<List<User>> GetUsersAsync();

    [Get("/users/{id}")]
    Task<User> GetUserByIdAsync(int id);

    [Post("/users")]
    Task<User> CreateUserAsync([Body] User user);
}
