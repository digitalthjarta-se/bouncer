using Bouncer.Core.Configuration;
using Bouncer.Core.Routing;

namespace Bouncer.Tests;

public class WebhookRouteResolverTests
{
    private static WebhookOptions BuildOptions(bool defaultEnabled = false) => new()
    {
        DefaultRoute = new DefaultWebhookRoute { Enabled = defaultEnabled, Url = "https://ops.example.com/hook" },
        Routes =
        [
            new WebhookRoute { From = "App1@Example.com", Url = "https://app1.example.com/hook", BearerToken = "token1" },
        ],
    };

    [Fact]
    public void Resolve_ExactCaseInsensitiveMatch_ReturnsMatchedRoute()
    {
        var result = WebhookRouteResolver.Resolve("app1@example.com", BuildOptions());

        Assert.Equal(WebhookRouteKind.Matched, result.Kind);
        Assert.Equal("app1@example.com", result.RouteKey);
        Assert.Equal("https://app1.example.com/hook", result.Url);
        Assert.Equal("token1", result.BearerToken);
    }

    [Fact]
    public void Resolve_NoMatchWithDefaultEnabled_ReturnsDefaultRoute()
    {
        var result = WebhookRouteResolver.Resolve("unknown@example.com", BuildOptions(defaultEnabled: true));

        Assert.Equal(WebhookRouteKind.Default, result.Kind);
        Assert.Equal(WebhookRouteResolver.DefaultRouteKey, result.RouteKey);
    }

    [Fact]
    public void Resolve_NoMatchNoDefault_ReturnsUnrouted()
    {
        var result = WebhookRouteResolver.Resolve("unknown@example.com", BuildOptions(defaultEnabled: false));

        Assert.Equal(WebhookRouteKind.Unrouted, result.Kind);
        Assert.Equal(WebhookRouteResolver.UnroutedRouteKey, result.RouteKey);
    }

    [Fact]
    public void Resolve_NullOriginalFromNoDefault_ReturnsUnrouted()
    {
        var result = WebhookRouteResolver.Resolve(null, BuildOptions(defaultEnabled: false));

        Assert.Equal(WebhookRouteKind.Unrouted, result.Kind);
    }
}
