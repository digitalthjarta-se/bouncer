namespace Bouncer.Core.Data.Models;

public static class MessageClassification
{
    public const string Unclassified = "unclassified";
    public const string Bounce = "bounce";
    public const string NonBounce = "non_bounce";
    public const string ParseError = "parse_error";
}

public sealed class ProcessedMessageRow
{
    public string Uidl { get; set; } = "";
    public string? MessageId { get; set; }
    public string FirstSeenAt { get; set; } = "";
    public string Classification { get; set; } = MessageClassification.Unclassified;
    public int ParseAttempts { get; set; }
    public bool DeletedFromMailbox { get; set; }
    public string? DeletedAt { get; set; }
}
