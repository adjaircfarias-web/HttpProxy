namespace HttpProxy.Interface;

/// <summary>
/// Proxy interface for map request
/// </summary>
/// <typeparam name="T">Must be an interface for mapping proxy requests</typeparam>
public interface IProxyFabric<T>
{
    T Proxy { get; }

    /// <summary>
    /// Set Headers parameters on HttpClient
    /// </summary>
    /// <param name="Headers">KeyValuePair List of parameters</param>
    IProxyFabric<T> WithHeaders(List<KeyValuePair<string, string>> Headers);

    /// <summary>
    /// Set Jwt Authorization for requests
    /// </summary>
    /// <param name="token">Token jwt</param>
    IProxyFabric<T> WithAuthorization(string? token);
    /// <summary>
    /// Set Basic Authorization for requests
    /// </summary>
    /// <param name="userCredential">User Credential</param>
    /// <param name="passwordCredential">Password Credential</param>
    IProxyFabric<T> WithBasicAuth(string? userCredential, string? passwordCredential);
    /// <summary>
    /// Set configuration for Retry Requests
    /// </summary>
    /// <param name="retryCount"></param>
    /// <param name="delaySeconds"></param>
    /// <param name="exponentialBackoff"></param>
    /// <returns></returns>
    IProxyFabric<T> WithRetryPolicy(int retryCount = 3, int delaySeconds = 2, bool exponentialBackoff = true);
    /// <summary>
    /// Set configuration for Circuit Braker 
    /// </summary>
    /// <param name="failureThreshold"></param>
    /// <param name="durationOfBreakSeconds"></param>
    /// <returns></returns>
    public IProxyFabric<T> WithCircuitBreaker(int failureThreshold = 5, int durationOfBreakSeconds = 30);
    /// <summary>
    /// Configura timeout para as requisições HTTP
    /// </summary>
    /// <param name="timeoutSeconds">Timeout em segundos (padrão: 30). Deve ser maior que 0</param>
    IProxyFabric<T> WithTimeout(int timeoutSeconds = 30);
}
