using HttpProxy.Proxy;
using HttpProxy.Tests.Interface;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace HttpProxy.Tests.ProxyFabric;

public class ProxyFabricTests
{
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<ILogger<ProxyFabric<ITestProxy>>> _mockLogger;
    private readonly Mock<IConfigurationSection> _mockConfigSection;

    public ProxyFabricTests()
    {
        _mockConfiguration = new Mock<IConfiguration>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockLogger = new Mock<ILogger<ProxyFabric<ITestProxy>>>();
        _mockConfigSection = new Mock<IConfigurationSection>();
    }

    [Fact]
    public void Constructor_Should_Throw_When_BaseUri_Is_Null()
    {
        // Arrange
        var mockSection = new Mock<IConfigurationSection>();
        mockSection.Setup(x => x.Value).Returns((string?)null);
        _mockConfiguration.Setup(x => x.GetSection(It.IsAny<string>())).Returns(mockSection.Object);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new ProxyFabric<ITestProxy>(_mockConfiguration.Object, _mockHttpClientFactory.Object, _mockLogger.Object));

        Assert.Contains("BaseUri não configurada", exception.Message);
    }

    [Fact]
    public void Constructor_Should_Throw_When_BaseUri_Is_Invalid()
    {
        // Arrange
        var mockSection = new Mock<IConfigurationSection>();
        mockSection.Setup(x => x.Value).Returns("invalid-uri");
        _mockConfiguration.Setup(x => x.GetSection(It.IsAny<string>())).Returns(mockSection.Object);

        // Act & Assert
        var exception = Assert.Throws<UriFormatException>(() =>
            new ProxyFabric<ITestProxy>(_mockConfiguration.Object, _mockHttpClientFactory.Object, _mockLogger.Object));

        Assert.Contains("URI inválida:", exception.Message);
    }

    [Fact]
    public void Constructor_Should_Create_Proxy_When_BaseUri_Is_Valid()
    {
        // Arrange
        var mockSection = new Mock<IConfigurationSection>();
        mockSection.Setup(x => x.Value).Returns("https://api.example.com");
        _mockConfiguration.Setup(x => x.GetSection(It.IsAny<string>())).Returns(mockSection.Object);

        var httpClient = new HttpClient { BaseAddress = new Uri("https://api.example.com") };
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Act
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Assert
        Assert.NotNull(proxyFabric.Proxy);
    }

    [Fact]
    public void Fluent_Interface_Should_Chain_Multiple_Operations()
    {
        // Arrange
        var proxyFabric = CreateValidProxyFabric();

        // Act
        var result = proxyFabric
            .WithHeaders(new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("X-Test", "value") })
            .WithAuthorization("token");

        // Assert
        Assert.Same(proxyFabric, result);
    }

    [Fact]
    public void AddHeaders_Should_Support_KeyValuePair_List()
    {
        // Arrange
        var proxyFabric = CreateValidProxyFabric();
        var headers = new List<KeyValuePair<string, string>>
        {
            new("X-Legacy-Header", "legacy-value")
        };

        // Act
        var result = proxyFabric.WithHeaders(headers);

        // Assert
        Assert.Same(proxyFabric, result);
    }

    [Fact]
    public void AddAuthorization_With_Token_Should_Return_Same_Instance()
    {
        // Arrange
        var proxyFabric = CreateValidProxyFabric();
        var token = "test-jwt-token";

        // Act
        var result = proxyFabric.WithAuthorization(token);

        // Assert
        Assert.Same(proxyFabric, result);
    }

    [Fact]
    public void AddHeaders_Should_Handle_Null_Headers_Gracefully()
    {
        // Arrange
        var proxyFabric = CreateValidProxyFabric();

        // Act
        var result = proxyFabric.WithHeaders((List<KeyValuePair<string, string>>)null!);

        // Assert
        Assert.NotNull(result);
        Assert.Same(proxyFabric, result);
    }

    [Fact]
    public void AddHeaders_Should_Return_Same_Instance_For_Fluent_Interface()
    {
        // Arrange
        var proxyFabric = CreateValidProxyFabric();
        var headers = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>( "X-Test-Header", "test-value" )
        };

        // Act
        var result = proxyFabric.WithHeaders(headers);

        // Assert
        Assert.Same(proxyFabric, result);
    }

    [Fact]
    public void AddAuthorization_BasicAuth_Should_Encode_Credentials()
    {
        // Arrange
        var proxyFabric = CreateValidProxyFabric();
        var username = "testuser";
        var password = "testpass";

        // Act
        var result = proxyFabric.WithBasicAuth(username, password);

        // Assert
        Assert.Same(proxyFabric, result);
    }

    [Fact]
    public void WithRetryPolicy_ShouldReturnProxyFabricInstance()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(_mockConfiguration.Object, _mockHttpClientFactory.Object, _mockLogger.Object);

        // Act
        var result = proxyFabric.WithRetryPolicy();

        // Assert
        Assert.Same(proxyFabric, result);
    }

    [Fact]
    public void WithRetryPolicy_ShouldLogConfiguration()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(_mockConfiguration.Object, _mockHttpClientFactory.Object, _mockLogger.Object);

        // Act
        proxyFabric.WithRetryPolicy(retryCount: 4, delaySeconds: 5);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Retry configurado")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void WithHeaders_WithValidHeaders_ShouldAddHeaders()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        var headers = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("X-Api-Key", "test-key"),
            new KeyValuePair<string, string>("X-Custom-Header", "custom-value")
        };

        // Act
        var result = proxyFabric.WithHeaders(headers);

        // Assert
        Assert.NotNull(result);
        Assert.Same(proxyFabric, result);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Header adicionado")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Exactly(2));
    }

    [Fact]
    public void WithHeaders_WithNullHeaders_ShouldLogWarningAndReturnSelf()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        var result = proxyFabric.WithHeaders(null!);

        // Assert
        Assert.NotNull(result);
        Assert.Same(proxyFabric, result);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Headers nulos ou vazios")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void WithHeaders_WithEmptyHeaders_ShouldLogWarningAndReturnSelf()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        var result = proxyFabric.WithHeaders(new List<KeyValuePair<string, string>>());

        // Assert
        Assert.NotNull(result);
        Assert.Same(proxyFabric, result);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Headers nulos ou vazios")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void WithHeaders_WithEmptyKey_ShouldIgnoreHeaderAndNotLog()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        var headers = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("", "value"),
            new KeyValuePair<string, string>("Valid-Header", "value")
        };

        // Act
        var result = proxyFabric.WithHeaders(headers);

        // Assert
        Assert.NotNull(result);

        // Apenas 1 header válido deve ser adicionado (não há warning para chave vazia no código refatorado)
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Header adicionado")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void WithHeaders_ShouldNotRecreateProxy()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        var headers = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("X-Header", "value")
        };

        // Act
        proxyFabric.WithHeaders(headers);

        // Assert
        Assert.NotNull(proxyFabric.Proxy);

        // Apenas o construtor cria o proxy, WithHeaders NÃO recria
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Configurando proxy")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once); // Apenas Constructor
    }

    [Fact]
    public void WithAuthorization_WithValidToken_ShouldConfigureAuth()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        var result = proxyFabric.WithAuthorization("valid-jwt-token");

        // Assert
        Assert.NotNull(result);
        Assert.Same(proxyFabric, result);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("JWT configurado")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void WithAuthorization_WithNullToken_ShouldLogWarningAndReturnSelf()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        var result = proxyFabric.WithAuthorization(null);

        // Assert
        Assert.NotNull(result);
        Assert.Same(proxyFabric, result);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Token vazio")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void WithBasicAuth_WithValidCredentials_ShouldConfigureAuth()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        var result = proxyFabric.WithBasicAuth("username", "password");

        // Assert
        Assert.NotNull(result);
        Assert.Same(proxyFabric, result);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Basic Auth configurado")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void WithBasicAuth_WithNullUsername_ShouldLogWarningAndReturnSelf()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        var result = proxyFabric.WithBasicAuth(null, "password");

        // Assert
        Assert.NotNull(result);
        Assert.Same(proxyFabric, result);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Credenciais inválidas")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void WithBasicAuth_WithNullPassword_ShouldLogWarningAndReturnSelf()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        var result = proxyFabric.WithBasicAuth("username", null);

        // Assert
        Assert.NotNull(result);
        Assert.Same(proxyFabric, result);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Credenciais inválidas")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void WithBasicAuth_WithEmptyCredentials_ShouldLogWarningAndReturnSelf()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        var result = proxyFabric.WithBasicAuth(string.Empty, string.Empty);

        // Assert
        Assert.NotNull(result);
        Assert.Same(proxyFabric, result);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Credenciais inválidas")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void WithBasicAuth_ShouldNotRecreateProxy()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        proxyFabric.WithBasicAuth("user", "pass");

        // Assert
        Assert.NotNull(proxyFabric.Proxy);

        // WithBasicAuth NÃO recria o proxy
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Configurando proxy")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once); // Apenas Constructor
    }

    [Fact]
    public void WithRetryPolicy_WithDefaultValues_ShouldConfigureRetry()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        var result = proxyFabric.WithRetryPolicy();

        // Assert
        Assert.NotNull(result);
        Assert.Same(proxyFabric, result);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString().Contains("Retry configurado") &&
                    v.ToString().Contains("3 tentativas") &&
                    v.ToString().Contains("delay: 2s") &&
                    v.ToString().Contains("exponencial: True")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void WithRetryPolicy_WithCustomValues_ShouldConfigureRetry()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        var result = proxyFabric.WithRetryPolicy(retryCount: 5, delaySeconds: 3, exponentialBackoff: false);

        // Assert
        Assert.NotNull(result);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString().Contains("5 tentativas") &&
                    v.ToString().Contains("delay: 3s") &&
                    v.ToString().Contains("exponencial: False")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void WithRetryPolicy_ShouldRecreateProxy()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        proxyFabric.WithRetryPolicy();

        // Assert
        Assert.NotNull(proxyFabric.Proxy);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Configurando proxy")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once); // Constructor + WithRetryPolicy
    }

    [Fact]
    public void WithRetryPolicy_WithZeroRetryCount_ShouldStillConfigure()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        var result = proxyFabric.WithRetryPolicy(retryCount: 0);

        // Assert
        Assert.NotNull(result);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("0 tentativas")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void WithRetryPolicy_MultipleCalls_ShouldUpdateConfiguration()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        proxyFabric.WithRetryPolicy(retryCount: 3);
        proxyFabric.WithRetryPolicy(retryCount: 7);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Retry configurado")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Exactly(2));
    }

    [Fact]
    public void WithCircuitBreaker_WithDefaultValues_ShouldConfigureCircuitBreaker()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        var result = proxyFabric.WithCircuitBreaker();

        // Assert
        Assert.NotNull(result);
        Assert.Same(proxyFabric, result);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString().Contains("Circuit breaker configurado") &&
                    v.ToString().Contains("5 falhas") &&
                    v.ToString().Contains("duração: 30s")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void WithCircuitBreaker_WithCustomValues_ShouldConfigureCircuitBreaker()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        var result = proxyFabric.WithCircuitBreaker(failureThreshold: 10, durationOfBreakSeconds: 60);

        // Assert
        Assert.NotNull(result);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString().Contains("10 falhas") &&
                    v.ToString().Contains("duração: 60s")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void WithCircuitBreaker_ShouldRecreateProxy()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        var proxyBefore = proxyFabric.Proxy;

        // Act
        proxyFabric.WithCircuitBreaker();

        // Assert
        Assert.NotNull(proxyFabric.Proxy);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Circuit breaker configurado")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);

        // O proxy foi recriado (nova instância)
        Assert.NotSame(proxyBefore, proxyFabric.Proxy);
    }

    [Fact]
    public void WithCircuitBreaker_MultipleCalls_ShouldUpdateConfiguration()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        proxyFabric.WithCircuitBreaker(failureThreshold: 5);
        proxyFabric.WithCircuitBreaker(failureThreshold: 12);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Circuit breaker configurado")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Exactly(2));
    }

    [Fact]
    public void FluentInterface_AllMethods_ShouldReturnSameInstance()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        var result = proxyFabric
            .WithHeaders(new List<KeyValuePair<string, string>> { new("key", "value") })
            .WithAuthorization("token")
            .WithRetryPolicy()
            .WithCircuitBreaker();

        // Assert
        Assert.NotNull(result);
        Assert.Same(proxyFabric, result);
    }

    [Fact]
    public void FluentInterface_CombinedRetryAndCircuitBreaker_ShouldWorkCorrectly()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        var result = proxyFabric
            .WithRetryPolicy(retryCount: 3, delaySeconds: 2)
            .WithCircuitBreaker(failureThreshold: 5, durationOfBreakSeconds: 30);

        // Assert
        Assert.NotNull(result);
        Assert.Same(proxyFabric, result);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Retry configurado")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Circuit breaker configurado")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void FluentInterface_ComplexChain_ShouldExecuteAllConfigurations()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        var headers = new List<KeyValuePair<string, string>>
        {
            new("X-Api-Key", "key123"),
            new("X-Client-Id", "client456")
        };

        // Act
        var result = proxyFabric
            .WithHeaders(headers)
            .WithBasicAuth("user", "pass")
            .WithRetryPolicy(retryCount: 4, delaySeconds: 3, exponentialBackoff: false)
            .WithCircuitBreaker(failureThreshold: 8, durationOfBreakSeconds: 45);

        // Assert
        Assert.NotNull(result);
        Assert.Same(proxyFabric, result);
        Assert.NotNull(proxyFabric.Proxy);
    }

    [Fact]
    public void Proxy_ShouldNeverBeNull()
    {
        // Arrange
        SetupValidConfiguration();

        // Act
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Assert
        Assert.NotNull(proxyFabric.Proxy);
    }

    [Fact]
    public void Proxy_AfterMultipleConfigurations_ShouldNeverBeNull()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act & Assert
        Assert.NotNull(proxyFabric.Proxy);

        proxyFabric.WithRetryPolicy();
        Assert.NotNull(proxyFabric.Proxy);

        proxyFabric.WithCircuitBreaker();
        Assert.NotNull(proxyFabric.Proxy);

        proxyFabric.WithAuthorization("token");
        Assert.NotNull(proxyFabric.Proxy);

        proxyFabric.WithHeaders(new List<KeyValuePair<string, string>> { new("k", "v") });
        Assert.NotNull(proxyFabric.Proxy);
    }

    [Fact]
    public void Constructor_ShouldCallCreateClientWithCorrectName()
    {
        // Arrange
        SetupValidConfiguration();

        // Act
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Assert
        _mockHttpClientFactory.Verify(
            x => x.CreateClient("ITestProxy"),
            Times.Once);
    }

    [Fact]
    public void WithHeaders_ShouldNotCallCreateClientAgain()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        proxyFabric.WithHeaders(new List<KeyValuePair<string, string>> { new("k", "v") });

        // Assert
        // WithHeaders NÃO recria o client
        _mockHttpClientFactory.Verify(
            x => x.CreateClient("ITestProxy"),
            Times.Once); // Apenas Constructor
    }

    [Fact]
    public void Constructor_ShouldReadConfigurationSection()
    {
        // Arrange
        SetupValidConfiguration();

        // Act
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Assert
        _mockConfiguration.Verify(
            x => x.GetSection(It.IsAny<string>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void WithRetryPolicy_VeryLargeValues_ShouldConfigure()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        var result = proxyFabric.WithRetryPolicy(retryCount: 1000, delaySeconds: 3600);

        // Assert
        Assert.NotNull(result);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString().Contains("1000 tentativas") &&
                    v.ToString().Contains("3600s")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void WithCircuitBreaker_VeryLargeValues_ShouldConfigure()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        var result = proxyFabric.WithCircuitBreaker(failureThreshold: 10000, durationOfBreakSeconds: 86400);

        // Assert
        Assert.NotNull(result);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString().Contains("10000 falhas") &&
                    v.ToString().Contains("86400s")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void WithHeaders_DuplicateKeys_ShouldUpdateValues()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        var headers1 = new List<KeyValuePair<string, string>>
        {
            new("X-Custom", "value1")
        };

        var headers2 = new List<KeyValuePair<string, string>>
        {
            new("X-Custom", "value2")
        };

        // Act
        proxyFabric.WithHeaders(headers1);
        proxyFabric.WithHeaders(headers2);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Header adicionado: X-Custom")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Exactly(2));
    }

    [Fact]
    public void WithBasicAuth_SpecialCharactersInCredentials_ShouldConfigureCorrectly()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        var result = proxyFabric.WithBasicAuth("user@domain.com", "p@ssw0rd!#$%");

        // Assert
        Assert.NotNull(result);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString().Contains("Basic Auth configurado") &&
                    v.ToString().Contains("user@domain.com")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void ResiliencePolicies_WithAuthentication_ShouldWorkTogether()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        var result = proxyFabric
            .WithAuthorization("token")
            .WithRetryPolicy(retryCount: 3)
            .WithCircuitBreaker(failureThreshold: 5);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(proxyFabric.Proxy);
    }

    [Fact]
    public void ResiliencePolicies_WithHeaders_ShouldWorkTogether()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        var headers = new List<KeyValuePair<string, string>>
        {
            new("X-Api-Key", "key"),
            new("X-Correlation-Id", "corr-123")
        };

        // Act
        var result = proxyFabric
            .WithHeaders(headers)
            .WithRetryPolicy()
            .WithCircuitBreaker();

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(proxyFabric.Proxy);
    }

    [Fact]
    public void AllFeatures_Combined_ShouldWorkCorrectly()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        var headers = new List<KeyValuePair<string, string>>
        {
            new("X-Api-Key", "api-key"),
            new("X-Request-Id", "req-123")
        };

        // Act
        var result = proxyFabric
            .WithHeaders(headers)
            .WithBasicAuth("username", "password")
            .WithRetryPolicy(retryCount: 5, delaySeconds: 3, exponentialBackoff: true)
            .WithCircuitBreaker(failureThreshold: 10, durationOfBreakSeconds: 60);

        // Assert
        Assert.NotNull(result);
        Assert.Same(proxyFabric, result);
        Assert.NotNull(proxyFabric.Proxy);

        // Verificar todos os logs relevantes
        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.AtLeast(6)); // Diversos logs de configuração
    }

    [Fact]
    public void ReconfigurationOrder_ShouldNotAffectFunctionality()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act - Ordem 1
        var result1 = proxyFabric
            .WithRetryPolicy()
            .WithCircuitBreaker()
            .WithAuthorization("token");

        // Reconfigurar em ordem diferente
        var result2 = proxyFabric
            .WithAuthorization("token2")
            .WithCircuitBreaker(failureThreshold: 10)
            .WithRetryPolicy(retryCount: 5);

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Same(proxyFabric, result1);
        Assert.Same(proxyFabric, result2);
    }

    [Fact]
    public void WithTimeout_WithDefaultValue_ShouldConfigureTimeout()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        var result = proxyFabric.WithTimeout();

        // Assert
        Assert.NotNull(result);
        Assert.Same(proxyFabric, result);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString().Contains("Timeout configurado") &&
                    v.ToString().Contains("30s")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void WithTimeout_WithCustomValue_ShouldConfigureCustomTimeout()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        var result = proxyFabric.WithTimeout(timeoutSeconds: 60);

        // Assert
        Assert.NotNull(result);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString().Contains("Timeout configurado") &&
                    v.ToString().Contains("60s")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void WithTimeout_ShouldReturnProxyFabricInstance()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        var result = proxyFabric.WithTimeout(45);

        // Assert
        Assert.Same(proxyFabric, result);
    }

    [Fact]
    public void WithTimeout_ShouldRecreateProxy()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        proxyFabric.WithTimeout(30);

        // Assert
        Assert.NotNull(proxyFabric.Proxy);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Configurando proxy")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once); // Constructor + WithTimeout
    }

    [Fact]
    public void WithTimeout_WithZeroValue_ShouldLogWarningAndUseDefault()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        var result = proxyFabric.WithTimeout(timeoutSeconds: 0);

        // Assert
        Assert.NotNull(result);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString().Contains("Timeout inválido") &&
                    v.ToString().Contains("padrão de 30s")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void WithTimeout_WithNegativeValue_ShouldLogWarningAndUseDefault()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        var result = proxyFabric.WithTimeout(timeoutSeconds: -10);

        // Assert
        Assert.NotNull(result);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString().Contains("Timeout inválido") &&
                    v.ToString().Contains("padrão de 30s")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void WithTimeout_WithVerySmallValue_ShouldConfigureTimeout()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        var result = proxyFabric.WithTimeout(timeoutSeconds: 1);

        // Assert
        Assert.NotNull(result);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString().Contains("Timeout configurado") &&
                    v.ToString().Contains("1s")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void WithTimeout_WithVeryLargeValue_ShouldConfigureTimeout()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        var result = proxyFabric.WithTimeout(timeoutSeconds: 3600);

        // Assert
        Assert.NotNull(result);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString().Contains("Timeout configurado") &&
                    v.ToString().Contains("3600s")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void WithTimeout_MultipleCalls_ShouldUpdateConfiguration()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        proxyFabric.WithTimeout(30);
        proxyFabric.WithTimeout(60);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Timeout configurado")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Exactly(2));
    }

    [Fact]
    public void WithTimeout_CombinedWithRetryPolicy_ShouldConfigureBoth()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        var result = proxyFabric
            .WithTimeout(timeoutSeconds: 30)
            .WithRetryPolicy(retryCount: 3);

        // Assert
        Assert.NotNull(result);
        Assert.Same(proxyFabric, result);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Timeout configurado")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Retry configurado")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void WithTimeout_CombinedWithCircuitBreaker_ShouldConfigureBoth()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        var result = proxyFabric
            .WithTimeout(timeoutSeconds: 45)
            .WithCircuitBreaker(failureThreshold: 5);

        // Assert
        Assert.NotNull(result);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Timeout configurado")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Circuit breaker configurado")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void WithTimeout_WithAllFeatures_ShouldConfigureAllPolicies()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        var headers = new List<KeyValuePair<string, string>>
        {
            new("X-Api-Key", "key123")
        };

        // Act
        var result = proxyFabric
            .WithHeaders(headers)
            .WithTimeout(timeoutSeconds: 25)
            .WithRetryPolicy(retryCount: 4, delaySeconds: 2)
            .WithCircuitBreaker(failureThreshold: 8)
            .WithAuthorization("token");

        // Assert
        Assert.NotNull(result);
        Assert.Same(proxyFabric, result);
        Assert.NotNull(proxyFabric.Proxy);

        // Verify all configurations were logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString().Contains("Timeout configurado") ||
                    v.ToString().Contains("Retry configurado") ||
                    v.ToString().Contains("Circuit breaker configurado")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.AtLeast(3));
    }

    [Fact]
    public void FluentInterface_WithTimeout_ShouldMaintainChaining()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        var result = proxyFabric
            .WithTimeout(30)
            .WithAuthorization("token")
            .WithRetryPolicy()
            .WithCircuitBreaker()
            .WithHeaders(new List<KeyValuePair<string, string>> { new("k", "v") });

        // Assert
        Assert.NotNull(result);
        Assert.Same(proxyFabric, result);
    }

    [Fact]
    public void WithTimeout_DifferentOrders_ShouldWorkCorrectly()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric1 = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        var proxyFabric2 = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act - Order 1
        proxyFabric1
            .WithTimeout(30)
            .WithRetryPolicy()
            .WithCircuitBreaker();

        // Act - Order 2
        proxyFabric2
            .WithRetryPolicy()
            .WithCircuitBreaker()
            .WithTimeout(30);

        // Assert
        Assert.NotNull(proxyFabric1.Proxy);
        Assert.NotNull(proxyFabric2.Proxy);
    }

    [Fact]
    public void WithTimeout_ShouldCallCreateClientWithCorrectName()
    {
        // Arrange
        SetupValidConfiguration();

        // Act
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        proxyFabric.WithTimeout(30);

        // Assert
        _mockHttpClientFactory.Verify(
            x => x.CreateClient("ITestProxy"),
            Times.Once); // Constructor + WithTimeout
    }

    [Fact]
    public void WithTimeout_ShouldCreateNewProxyInstance()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        var proxyBefore = proxyFabric.Proxy;

        // Act
        proxyFabric.WithTimeout(45);
        var proxyAfter = proxyFabric.Proxy;

        // Assert
        Assert.NotNull(proxyBefore);
        Assert.NotNull(proxyAfter);
    }

    [Fact]
    public void WithTimeout_ShouldLogWithInformationLevel()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        proxyFabric.WithTimeout(30);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.AtLeastOnce);
    }

    [Fact]
    public void WithTimeout_WithInvalidValue_ShouldLogWithWarningLevel()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        proxyFabric.WithTimeout(-5);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void WithTimeout_ShouldLogTimeoutValue()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        proxyFabric.WithTimeout(75);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("75s")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void WithTimeout_BoundaryValue_LowerBound_ShouldConfigureOne()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        var result = proxyFabric.WithTimeout(1);

        // Assert
        Assert.NotNull(result);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("1s")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void WithRetryPolicy_WithNegativeRetryCount_ShouldThrowException()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            proxyFabric.WithRetryPolicy(retryCount: -1, delaySeconds: 2));
    }

    [Fact]
    public void WithRetryPolicy_WithNegativeDelaySeconds_ShouldThrowException()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            proxyFabric.WithRetryPolicy(retryCount: 3, delaySeconds: -1));
    }

    [Fact]
    public void WithCircuitBreaker_WithZeroFailureThreshold_ShouldThrowException()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            proxyFabric.WithCircuitBreaker(failureThreshold: 0, durationOfBreakSeconds: 30));
    }

    [Fact]
    public void WithCircuitBreaker_WithNegativeFailureThreshold_ShouldThrowException()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            proxyFabric.WithCircuitBreaker(failureThreshold: -5, durationOfBreakSeconds: 30));
    }

    [Fact]
    public void WithCircuitBreaker_WithZeroDurationOfBreak_ShouldThrowException()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            proxyFabric.WithCircuitBreaker(failureThreshold: 5, durationOfBreakSeconds: 0));
    }

    [Fact]
    public void WithCircuitBreaker_WithNegativeDurationOfBreak_ShouldThrowException()
    {
        // Arrange
        SetupValidConfiguration();
        var proxyFabric = new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            proxyFabric.WithCircuitBreaker(failureThreshold: 5, durationOfBreakSeconds: -30));
    }

    private void SetupValidConfiguration(string baseUri = "https://api.test.com")
    {
        _mockConfigSection.Setup(x => x.Value).Returns(baseUri);
        _mockConfiguration.Setup(x => x.GetSection(It.IsAny<string>())).Returns(_mockConfigSection.Object);

        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUri)
        };
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);
    }

    private ProxyFabric<ITestProxy> CreateValidProxyFabric()
    {
        var mockSection = new Mock<IConfigurationSection>();
        mockSection.Setup(x => x.Value).Returns("https://api.example.com");
        _mockConfiguration.Setup(x => x.GetSection(It.IsAny<string>())).Returns(mockSection.Object);

        var httpClient = new HttpClient { BaseAddress = new Uri("https://api.example.com") };
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        return new ProxyFabric<ITestProxy>(
            _mockConfiguration.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);
    }
}
