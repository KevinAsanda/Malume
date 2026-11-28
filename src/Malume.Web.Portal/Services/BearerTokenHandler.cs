using System.Net.Http.Headers;
using Malume.Shared.Contracts.Auth;

namespace Malume.Web.Portal.Services;

public sealed class BearerTokenHandler(TokenStore tokenStore) : DelegatingHandler
{
    private readonly TokenStore _tokenStore = tokenStore;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        AuthResponse? auth = await _tokenStore.GetAsync(cancellationToken);
        if (auth is not null
            && !string.IsNullOrWhiteSpace(auth.AccessToken)
            && request.Headers.Authorization is null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
