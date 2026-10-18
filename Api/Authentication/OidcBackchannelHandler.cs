namespace BuildingCopilot.Api.Authentication;

public sealed class OidcBackchannelHandler(
    Uri publicAuthority,
    Uri internalAuthority) : DelegatingHandler(new SocketsHttpHandler())
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri is { } requestUri &&
            requestUri.Host.Equals(publicAuthority.Host, StringComparison.OrdinalIgnoreCase) &&
            requestUri.Port == publicAuthority.Port)
        {
            var uriBuilder = new UriBuilder(requestUri)
            {
                Scheme = internalAuthority.Scheme,
                Host = internalAuthority.Host,
                Port = internalAuthority.IsDefaultPort ? -1 : internalAuthority.Port
            };
            request.RequestUri = uriBuilder.Uri;
        }

        return base.SendAsync(request, cancellationToken);
    }
}