using Microsoft.Extensions.Options;

namespace Bouncer.Core.Configuration;

public sealed class BouncerOptionsValidator : IValidateOptions<BouncerOptions>
{
    public ValidateOptionsResult Validate(string? name, BouncerOptions options)
    {
        var errors = new List<string>();

        if (options.PollIntervalSeconds <= 0)
            errors.Add("PollIntervalSeconds must be greater than 0.");

        if (string.IsNullOrWhiteSpace(options.Database.Path))
            errors.Add("Database:Path must be set.");

        if (string.IsNullOrWhiteSpace(options.Pop3.Host))
            errors.Add("Pop3:Host must be set.");

        if (options.Pop3.Port is <= 0 or > 65535)
            errors.Add("Pop3:Port must be between 1 and 65535.");

        if (string.IsNullOrWhiteSpace(options.Pop3.Username))
            errors.Add("Pop3:Username must be set.");

        if (options.ParseFailure.MaxAttempts <= 0)
            errors.Add("ParseFailure:MaxAttempts must be greater than 0.");

        ValidateRetry(options.Retry, errors);
        ValidateWebhooks(options.Webhooks, errors);

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }

    private static void ValidateRetry(RetryOptions retry, List<string> errors)
    {
        if (retry.InitialBackoffSeconds <= 0)
            errors.Add("Retry:InitialBackoffSeconds must be greater than 0.");

        if (retry.BackoffMultiplier <= 1.0)
            errors.Add("Retry:BackoffMultiplier must be greater than 1.0.");

        if (retry.MaxBackoffSeconds < retry.InitialBackoffSeconds)
            errors.Add("Retry:MaxBackoffSeconds must be greater than or equal to Retry:InitialBackoffSeconds.");
    }

    private static void ValidateWebhooks(WebhookOptions webhooks, List<string> errors)
    {
        if (webhooks.TimeoutSeconds <= 0)
            errors.Add("Webhooks:TimeoutSeconds must be greater than 0.");

        if (webhooks.MaxBatchSize <= 0)
            errors.Add("Webhooks:MaxBatchSize must be greater than 0.");

        if (webhooks.DefaultRoute.Enabled && !IsAbsoluteHttpUrl(webhooks.DefaultRoute.Url))
            errors.Add("Webhooks:DefaultRoute:Url must be an absolute http(s) URL when DefaultRoute is enabled.");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var route in webhooks.Routes)
        {
            if (string.IsNullOrWhiteSpace(route.From))
            {
                errors.Add("Webhooks:Routes entries must specify a non-empty From address.");
                continue;
            }

            if (!seen.Add(route.From))
                errors.Add($"Webhooks:Routes contains a duplicate From address: '{route.From}'.");

            if (!IsAbsoluteHttpUrl(route.Url))
                errors.Add($"Webhooks:Routes entry for '{route.From}' must have an absolute http(s) Url.");
        }
    }

    private static bool IsAbsoluteHttpUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
