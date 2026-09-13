namespace Bouncer.Core.Configuration;

public sealed class BouncerOptions
{
    public const string SectionName = "Bouncer";

    public int PollIntervalSeconds { get; set; } = 60;
    public DatabaseOptions Database { get; set; } = new();
    public Pop3Options Pop3 { get; set; } = new();
    public RetryOptions Retry { get; set; } = new();
    public WebhookOptions Webhooks { get; set; } = new();
    public ParseFailureOptions ParseFailure { get; set; } = new();
}

public sealed class DatabaseOptions
{
    public string Path { get; set; } = "bouncer.db";
}

public sealed class ParseFailureOptions
{
    public int MaxAttempts { get; set; } = 3;
}
