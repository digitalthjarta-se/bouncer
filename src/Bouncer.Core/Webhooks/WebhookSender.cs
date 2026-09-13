using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Bouncer.Core.Webhooks;

public sealed record WebhookPostResult(bool Success, string? Error)
{
    public static WebhookPostResult Ok() => new(true, null);
    public static WebhookPostResult Failed(string error) => new(false, error);
}

public sealed class WebhookSender(HttpClient httpClient)
{
    public async Task<WebhookPostResult> PostAsync(
        string url, string? bearerToken, BouncePayload payload, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload),
        };
        if (!string.IsNullOrEmpty(bearerToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            using var response = await httpClient.SendAsync(request, cts.Token);
            return response.IsSuccessStatusCode
                ? WebhookPostResult.Ok()
                : WebhookPostResult.Failed($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return WebhookPostResult.Failed($"Timed out after {timeout.TotalSeconds}s");
        }
        catch (Exception ex)
        {
            return WebhookPostResult.Failed(ex.Message);
        }
    }
}
