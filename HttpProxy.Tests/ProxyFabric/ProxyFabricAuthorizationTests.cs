using HttpProxy.Proxy;
using HttpProxy.ProxyAttribute;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Refit;

namespace HttpProxy.Tests.ProxyFabric;

public class ProxyFabricAuthorizationTests
{
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IHttpClientFactory> _mockClientFactory;
    private readonly Mock<ILogger<ProxyFabric<ITestApi>>> _mockLogger;

    public ProxyFabricAuthorizationTests()
    {
        _mockConfiguration = new Mock<IConfiguration>();
        _mockClientFactory = new Mock<IHttpClientFactory>();
        _mockLogger = new Mock<ILogger<ProxyFabric<ITestApi>>>();

        SetupValidConfiguration();
    }

    [Fact]
    public void WithAuthorization_With_Valid_Token_Should_Apply_Bearer_Token()
    {
        // Arrange
        var proxyFabric = CreateValidProxyFabric();
        var token = "valid-jwt-token";

        // Act
        var result = proxyFabric.WithAuthorization(token);

        // Assert
        Assert.Same(proxyFabric, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void WithAuthorization_With_Invalid_Token_Should_Log_Warning_And_Return_Same_Instance(
        string? invalidToken)
    {
        // Arrange
        var proxyFabric = CreateValidProxyFabric();

        // Act
        var result = proxyFabric.WithAuthorization(invalidToken);

        // Assert
        Assert.Same(proxyFabric, result);
    }

    [Fact]
    public void WithBasicAuth_With_Valid_Credentials_Should_Apply_Basic_Auth()
    {
        // Arrange
        var proxyFabric = CreateValidProxyFabric();
        var username = "admin";
        var password = "secret123";

        // Act
        var result = proxyFabric.WithBasicAuth(username, password);

        // Assert
        Assert.Same(proxyFabric, result);
    }

    [Theory]
    [InlineData(null, "password")]
    [InlineData("username", null)]
    [InlineData("", "password")]
    [InlineData("username", "")]
    [InlineData("   ", "password")]
    [InlineData("username", "   ")]
    public void WithBasicAuth_With_Invalid_Credentials_Should_Log_Warning(
        string? username,
        string? password)
    {
        // Arrange
        var proxyFabric = CreateValidProxyFabric();

        // Act
        var result = proxyFabric.WithBasicAuth(username, password);

        // Assert
        Assert.Same(proxyFabric, result);
    }

    private ProxyFabric<ITestApi> CreateValidProxyFabric()
    {
        return new ProxyFabric<ITestApi>(
            _mockConfiguration.Object,
            _mockClientFactory.Object,
            _mockLogger.Object);
    }

    private void SetupValidConfiguration()
    {
        var mockSection = new Mock<IConfigurationSection>();
        mockSection.Setup(x => x.Value).Returns("https://api.example.com");

        _mockConfiguration
            .Setup(x => x.GetSection(It.IsAny<string>()))
            .Returns(mockSection.Object);

        var mockHttpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.example.com")
        };

        _mockClientFactory
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(mockHttpClient);
    }
}

[ProxyBaseUri("ApiSettings:BaseUrl")]
public interface ITestApi
{
    [Get("/users")]
    Task<List<User>> GetUsersAsync();
}
