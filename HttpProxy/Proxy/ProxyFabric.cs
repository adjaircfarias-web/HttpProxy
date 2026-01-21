using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using HttpProxy.Constants;
using HttpProxy.Handlers;
using HttpProxy.Interface;
using HttpProxy.ProxyAttribute;
using HttpProxy.ResiliencePolicy;
using Refit;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace HttpProxy.Proxy;

public class ProxyFabric<T> : IProxyFabric<T>, IDisposable
{
    public T Proxy { get; private set; }
    private readonly IConfiguration _configuration;
    private readonly ILogger<ProxyFabric<T>> _logger;
    private readonly IHttpClientFactory _clientFactory;
    private readonly string _clientName;

    private readonly ConcurrentDictionary<string, string> _headers;
    private AuthenticationHeaderValue? _authHeader;
    private ResiliencePolicyConfig _policyConfig;

    private HttpClient _httpClient;
    private IAsyncPolicy<HttpResponseMessage>? _combinedPolicy;
    private bool _disposed;
    private readonly List<IDisposable> _disposables = new();

    public ProxyFabric(
        IConfiguration configuration,
        IHttpClientFactory clientFactory,
        ILogger<ProxyFabric<T>> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _clientName = typeof(T).Name;
        _headers = new ConcurrentDictionary<string, string>();
        _policyConfig = new ResiliencePolicyConfig();

        Proxy = CreateProxy();
    }

    private T CreateProxy()
    {
        var baseUri = GetBaseUri();
        _logger.LogInformation(
            "Configurando proxy para {ProxyType} com base URI: {BaseUri}",
            typeof(T).Name, baseUri);

        _httpClient = CreateHttpClient(baseUri);

        return CreateRefitClient();
    }

    private T CreateRefitClient()
    {
        var innerHandler = new HttpClientHandler();
        var policyHandler = new PolicyHttpMessageHandler(_combinedPolicy)
        {
            InnerHandler = innerHandler
        };

        _disposables.Add(policyHandler);

        var refitSettings = new RefitSettings
        {
            HttpMessageHandlerFactory = () => policyHandler
        };

        return RestService.For<T>(_httpClient, refitSettings);
    }

    private HttpClient CreateHttpClient(Uri baseAddress)
    {
        var httpClient = _clientFactory.CreateClient(_clientName);
        httpClient.BaseAddress = baseAddress;

        // Timeout alto para não interferir com Polly
        httpClient.Timeout = Timeout.InfiniteTimeSpan;

        ConfigureDefaultHeaders(httpClient);

        return httpClient;
    }

    private void ConfigureDefaultHeaders(HttpClient httpClient)
    {
        httpClient.DefaultRequestHeaders.Accept.Clear();
        httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue(ConstantsProxy.MediaApplication));

        ApplyCustomHeaders(httpClient);
        ApplyAuthorization(httpClient);
    }

    private void ApplyCustomHeaders(HttpClient httpClient)
    {
        foreach (var (key, value) in _headers)
        {
            httpClient.DefaultRequestHeaders.Remove(key);
            httpClient.DefaultRequestHeaders.Add(key, value);
        }
    }

    private void ApplyAuthorization(HttpClient httpClient)
    {
        if (_authHeader is not null)
        {
            httpClient.DefaultRequestHeaders.Authorization = _authHeader;
        }
    }

    public IProxyFabric<T> WithTimeout(int timeoutSeconds = 30)
    {
        if (timeoutSeconds <= 0)
        {
            _logger.LogWarning(
                "Timeout inválido: {Timeout}s. Usando padrão de 30s.",
                timeoutSeconds);
            timeoutSeconds = 30;
        }

        _policyConfig.TimeoutSeconds = timeoutSeconds;
        _policyConfig.EnableTimeout = true;

        _logger.LogInformation("Timeout configurado: {Timeout}s", timeoutSeconds);

        RebuildPolicies();
        return this;
    }

    public IProxyFabric<T> WithRetryPolicy(int retryCount = 3, int delaySeconds = 2, bool exponentialBackoff = true)
    {
        if (retryCount < 0 || delaySeconds < 0)
        {
            throw new ArgumentException("RetryCount e DelaySeconds devem ser positivos");
        }

        _policyConfig.RetryCount = retryCount;
        _policyConfig.RetryDelaySeconds = delaySeconds;
        _policyConfig.ExponentialBackoff = exponentialBackoff;
        _policyConfig.EnableRetry = true;

        _logger.LogInformation(
            "Retry configurado: {Count} tentativas, delay: {Delay}s, exponencial: {Exponential}",
            retryCount, delaySeconds, exponentialBackoff);

        RebuildPolicies();
        return this;
    }

    public IProxyFabric<T> WithCircuitBreaker(int failureThreshold = 5, int durationOfBreakSeconds = 30)
    {
        if (failureThreshold <= 0 || durationOfBreakSeconds <= 0)
        {
            throw new ArgumentException("Valores devem ser positivos");
        }

        _policyConfig.FailureThreshold = failureThreshold;
        _policyConfig.DurationOfBreakSeconds = durationOfBreakSeconds;
        _policyConfig.EnableCircuitBreaker = true;

        _logger.LogInformation(
            "Circuit breaker configurado: {Threshold} falhas, duração: {Duration}s",
            failureThreshold, durationOfBreakSeconds);

        RebuildPolicies();
        return this;
    }

    //reconstrói políticas e o proxy
    private void RebuildPolicies()
    {
        _combinedPolicy = BuildCombinedPolicy();

        Proxy = RestService.For<T>(_httpClient, new RefitSettings
        {
            HttpMessageHandlerFactory = () => new PolicyHttpMessageHandler(_combinedPolicy)
            {
                InnerHandler = new HttpClientHandler()
            }
        });
    }

    //Combina as políticas ativas já configuradas
    private IAsyncPolicy<HttpResponseMessage> BuildCombinedPolicy()
    {
        var policies = new List<IAsyncPolicy<HttpResponseMessage>>();

        // 1. Timeout (mais interno - executa primeiro)
        if (_policyConfig.EnableTimeout)
        {
            policies.Add(BuildTimeoutPolicy());
        }

        // 2. Retry (meio)
        if (_policyConfig.EnableRetry)
        {
            policies.Add(BuildRetryPolicy());
        }

        // 3. Circuit Breaker (mais externo)
        if (_policyConfig.EnableCircuitBreaker)
        {
            policies.Add(BuildCircuitBreakerPolicy());
        }

        // Se não tiver políticas, retorna NoOp
        if (!policies.Any())
        {
            return Policy.NoOpAsync<HttpResponseMessage>();
        }

        // Combina todas as políticas em ordem (de fora para dentro)
        return policies.Count == 1
            ? policies[0]
            : Policy.WrapAsync(policies.ToArray());
    }

    private IAsyncPolicy<HttpResponseMessage> BuildTimeoutPolicy()
    {
        return Policy.TimeoutAsync<HttpResponseMessage>(
            TimeSpan.FromSeconds(_policyConfig.TimeoutSeconds),
            onTimeoutAsync: (context, timespan, task) =>
            {
                _logger.LogWarning(
                    "Timeout de {Timeout}s excedido para {ProxyType}",
                    timespan.TotalSeconds,
                    typeof(T).Name);
                return Task.CompletedTask;
            });
    }

    private IAsyncPolicy<HttpResponseMessage> BuildRetryPolicy()
    {
        var sleepDurations = _policyConfig.ExponentialBackoff
            ? Enumerable.Range(1, _policyConfig.RetryCount)
                .Select(i => TimeSpan.FromSeconds(Math.Pow(2, i - 1) * _policyConfig.RetryDelaySeconds))
            : Enumerable.Repeat(TimeSpan.FromSeconds(_policyConfig.RetryDelaySeconds), _policyConfig.RetryCount);

        return Policy
            .HandleResult<HttpResponseMessage>(r =>
                r.StatusCode >= HttpStatusCode.InternalServerError ||
                r.StatusCode == HttpStatusCode.RequestTimeout ||
                r.StatusCode == HttpStatusCode.ServiceUnavailable)
            .Or<HttpRequestException>()
            .Or<TaskCanceledException>()
            .Or<TimeoutException>()
            .WaitAndRetryAsync(
                sleepDurations,
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    var statusCode = outcome.Result?.StatusCode.ToString() ??
                                    outcome.Exception?.GetType().Name ??
                                    "Unknown";

                    _logger.LogWarning(
                        "Retry {RetryCount}/{MaxRetries} para {ProxyType}. " +
                        "Aguardando {Delay}s. Motivo: {Reason}",
                        retryCount,
                        _policyConfig.RetryCount,
                        timespan.TotalSeconds,
                        statusCode);
                });
    }

    private IAsyncPolicy<HttpResponseMessage> BuildCircuitBreakerPolicy()
    {
        return Policy
            .HandleResult<HttpResponseMessage>(r =>
                r.StatusCode >= HttpStatusCode.InternalServerError ||
                r.StatusCode == HttpStatusCode.RequestTimeout ||
                r.StatusCode == HttpStatusCode.ServiceUnavailable)
            .Or<HttpRequestException>()
            .Or<TaskCanceledException>()
            .Or<TimeoutException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: _policyConfig.FailureThreshold,
                durationOfBreak: TimeSpan.FromSeconds(_policyConfig.DurationOfBreakSeconds),
                onBreak: (outcome, duration) =>
                {
                    var statusCode = outcome.Result?.StatusCode.ToString() ??
                                    outcome.Exception?.GetType().Name ??
                                    "Unknown";

                    _logger.LogError(
                        "🔴 Circuit Breaker ABERTO para {ProxyType}! " +
                        "Duração: {Duration}s. Motivo: {Reason}",
                        typeof(T).Name,
                        duration.TotalSeconds,
                        statusCode);
                },
                onReset: () =>
                {
                    _logger.LogInformation(
                        "🟢 Circuit Breaker RESETADO para {ProxyType}",
                        typeof(T).Name);
                },
                onHalfOpen: () =>
                {
                    _logger.LogInformation(
                        "🟡 Circuit Breaker SEMI-ABERTO para {ProxyType}. Testando serviço...",
                        typeof(T).Name);
                });
    }

    public IProxyFabric<T> WithHeaders(List<KeyValuePair<string, string>> headers)
    {
        if (headers?.Any() != true)
        {
            _logger.LogWarning("Headers nulos ou vazios");
            return this;
        }

        foreach (var header in headers.Where(h => !string.IsNullOrWhiteSpace(h.Key)))
        {
            _headers.AddOrUpdate(header.Key, header.Value, (_, _) => header.Value);
            _logger.LogDebug("Header adicionado: {Key}", header.Key);
        }

        ApplyCustomHeaders(_httpClient);
        return this;
    }

    public IProxyFabric<T> WithAuthorization(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Token vazio");
            return this;
        }

        _authHeader = new AuthenticationHeaderValue(ConstantsProxy.Bearer, token);
        _logger.LogDebug("JWT configurado");

        ApplyAuthorization(_httpClient);
        return this;
    }

    public IProxyFabric<T> WithBasicAuth(string? username, string? password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("Credenciais inválidas");
            return this;
        }

        var credentials = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{username}:{password}"));

        _authHeader = new AuthenticationHeaderValue("Basic", credentials);
        _logger.LogDebug("Basic Auth configurado para: {User}", username);

        ApplyAuthorization(_httpClient);
        return this;
    }

    private Uri GetBaseUri()
    {
        var attribute = ProxyBaseUriAttribute.GetCustomAttribute(typeof(T))
            ?? throw new InvalidOperationException(
                $"Interface {typeof(T).Name} deve ter [ProxyBaseUri]");

        var uriString = _configuration.GetSection(attribute.BaseUri).Value;

        if (string.IsNullOrWhiteSpace(uriString))
        {
            throw new ArgumentException(
                $"BaseUri não configurada para {typeof(T).Name}");
        }

        if (!Uri.TryCreate(uriString, UriKind.Absolute, out var uri))
        {
            throw new UriFormatException($"URI inválida: {uriString}");
        }

        return uri;
    }

    public void Dispose()
    {
        if (_disposed) return;

        foreach (var disposable in _disposables)
        {
            disposable?.Dispose();
        }

        _disposed = true;
    }
}
