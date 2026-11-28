using System.Text.Json;
using Malume.Shared.Contracts.Auth;
using Microsoft.JSInterop;

namespace Malume.Web.Portal.Services;

public sealed class TokenStore(IJSRuntime jsRuntime)
{
    private const string StorageKey = "malume.auth";

    private readonly IJSRuntime _jsRuntime = jsRuntime;
    private AuthResponse? _cached;

    public async Task<AuthResponse?> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        string? json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", cancellationToken, StorageKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            _cached = JsonSerializer.Deserialize<AuthResponse>(json);
            return _cached;
        }
        catch (JsonException)
        {
            await ClearAsync(cancellationToken);
            return null;
        }
    }

    public async Task SaveAsync(AuthResponse auth, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auth);

        _cached = auth;
        string json = JsonSerializer.Serialize(auth);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", cancellationToken, StorageKey, json);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _cached = null;
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", cancellationToken, StorageKey);
    }
}
