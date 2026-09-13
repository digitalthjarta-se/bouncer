namespace Bouncer.Core.Configuration;

public sealed class WebhookRoute
{
    public string From { get; set; } = "";
    public string Url { get; set; } = "";
    public string? BearerToken { get; set; }
    public string? BearerTokenEnvVar { get; set; }

    public string? ResolveBearerToken() => SecretResolver.Resolve(BearerToken, BearerTokenEnvVar);
}

public sealed class DefaultWebhookRoute
{
    public bool Enabled { get; set; }
    public string? Url { get; set; }
    public string? BearerToken { get; set; }
    public string? BearerTokenEnvVar { get; set; }

    public string? ResolveBearerToken() => SecretResolver.Resolve(BearerToken, BearerTokenEnvVar);
}
