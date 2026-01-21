using Polly;

namespace HttpProxy.Handlers;

//Integra o Polly com o Refit
internal class PolicyHttpMessageHandler : DelegatingHandler
{
    private readonly IAsyncPolicy<HttpResponseMessage>? _policy;

    public PolicyHttpMessageHandler(IAsyncPolicy<HttpResponseMessage>? policy)
    {
        _policy = policy;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (_policy == null)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        return await _policy.ExecuteAsync(
            ct => base.SendAsync(request, ct),
            cancellationToken);
    }
}
