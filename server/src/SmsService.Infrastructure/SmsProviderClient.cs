using System.Net.Http.Json;
using SmsService.Application;

namespace SmsService.Infrastructure;

public class SmsProviderOptions
{
    public const string SectionName = "SmsProvider";
    public string BaseUrl { get; set; } = "http://localhost:5100";
    public int TimeoutSeconds { get; set; } = 5;
}

/// <summary>HTTP client for the fake provider contract: POST /send, GET /status/{id}.</summary>
public class SmsProviderClient : ISmsProviderClient
{
    private readonly HttpClient _http;

    public SmsProviderClient(HttpClient http) => _http = http;

    private record SendRequest(string To, string Body);
    private record SendResponse(string ProviderMessageId);
    private record StatusResponse(string Status);

    public async Task<string> SendAsync(string to, string body, CancellationToken ct)
    {
        var response = await CallAsync(() => _http.PostAsJsonAsync("send", new SendRequest(to, body), ct), ct);
        var payload = await response.Content.ReadFromJsonAsync<SendResponse>(ct);
        if (string.IsNullOrWhiteSpace(payload?.ProviderMessageId))
            throw new SmsProviderException("Provider returned no providerMessageId.", isTransient: false);
        return payload.ProviderMessageId;
    }

    public async Task<ProviderStatus> GetStatusAsync(string providerMessageId, CancellationToken ct)
    {
        var response = await CallAsync(() => _http.GetAsync($"status/{Uri.EscapeDataString(providerMessageId)}", ct), ct);
        var payload = await response.Content.ReadFromJsonAsync<StatusResponse>(ct);
        if (payload is null || !Enum.TryParse<ProviderStatus>(payload.Status, ignoreCase: true, out var status))
            throw new SmsProviderException($"Unknown provider status '{payload?.Status}'.", isTransient: false);
        return status;
    }

    /// <summary>Maps HTTP failures: network error, timeout and 5xx are transient; other non-2xx are not.</summary>
    private static async Task<HttpResponseMessage> CallAsync(Func<Task<HttpResponseMessage>> call, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await call();
        }
        catch (HttpRequestException ex)
        {
            throw new SmsProviderException("Provider unreachable.", isTransient: true, ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new SmsProviderException("Provider call timed out.", isTransient: true, ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var code = (int)response.StatusCode;
            throw new SmsProviderException($"Provider returned HTTP {code}.", isTransient: code >= 500);
        }
        return response;
    }
}
