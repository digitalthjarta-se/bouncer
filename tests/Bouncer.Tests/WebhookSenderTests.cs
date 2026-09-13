using System.Net;
using Bouncer.Core.Webhooks;

namespace Bouncer.Tests;

public class WebhookSenderTests
{
    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(respond(request));
        }
    }

    private static BouncePayload SamplePayload() => new(
        "app1@example.com",
        [new BouncePayloadItem("app1@example.com", "fail@nowhere.invalid", null, "failed", "5.1.1", null, null, DateTime.UtcNow.ToString("o"))]);

    [Fact]
    public async Task PostAsync_SuccessResponse_ReturnsSuccess()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var sender = new WebhookSender(new HttpClient(handler));

        var result = await sender.PostAsync("https://example.com/hook", null, SamplePayload(), TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task PostAsync_NonSuccessStatusCode_ReturnsFailureWithStatus()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var sender = new WebhookSender(new HttpClient(handler));

        var result = await sender.PostAsync("https://example.com/hook", null, SamplePayload(), TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("500", result.Error);
    }

    [Fact]
    public async Task PostAsync_WithBearerToken_SetsAuthorizationHeader()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var sender = new WebhookSender(new HttpClient(handler));

        await sender.PostAsync("https://example.com/hook", "secret-token", SamplePayload(), TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.Equal("Bearer", handler.LastRequest!.Headers.Authorization!.Scheme);
        Assert.Equal("secret-token", handler.LastRequest!.Headers.Authorization!.Parameter);
    }

    [Fact]
    public async Task PostAsync_HandlerThrows_ReturnsFailureWithoutThrowing()
    {
        var handler = new FakeHandler(_ => throw new HttpRequestException("connection refused"));
        var sender = new WebhookSender(new HttpClient(handler));

        var result = await sender.PostAsync("https://example.com/hook", null, SamplePayload(), TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("connection refused", result.Error);
    }
}
