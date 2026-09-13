using Bouncer.Core.Configuration;

namespace Bouncer.Core.Routing;

public enum WebhookRouteKind
{
    Matched,
    Default,
    Unrouted,
}

public sealed record ResolvedWebhookRoute(string RouteKey, WebhookRouteKind Kind, string? Url, string? BearerToken);

public static class WebhookRouteResolver
{
    public const string DefaultRouteKey = "__default__";
    public const string UnroutedRouteKey = "dropped_unrouted";

    /// <summary>
    /// Resolves a bounce's original-From address to a webhook route: an exact (case-insensitive)
    /// configured match, the catch-all default route if enabled, or "unrouted" if neither applies.
    /// </summary>
    public static ResolvedWebhookRoute Resolve(string? originalFrom, WebhookOptions options)
    {
        if (!string.IsNullOrEmpty(originalFrom))
        {
            var match = options.Routes.FirstOrDefault(r => string.Equals(r.From, originalFrom, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                return new ResolvedWebhookRoute(match.From.ToLowerInvariant(), WebhookRouteKind.Matched, match.Url, match.ResolveBearerToken());
        }

        if (options.DefaultRoute.Enabled)
            return new ResolvedWebhookRoute(DefaultRouteKey, WebhookRouteKind.Default, options.DefaultRoute.Url, options.DefaultRoute.ResolveBearerToken());

        return new ResolvedWebhookRoute(UnroutedRouteKey, WebhookRouteKind.Unrouted, null, null);
    }
}
