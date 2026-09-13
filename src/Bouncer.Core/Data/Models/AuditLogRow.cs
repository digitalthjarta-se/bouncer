namespace Bouncer.Core.Data.Models;

public static class AuditReason
{
    public const string NonBounce = "non_bounce";
    public const string ParseErrorExhausted = "parse_error_exhausted";
    public const string DroppedUnrouted = "dropped_unrouted";
}

public sealed class AuditLogRow
{
    public long Id { get; set; }
    public string Uidl { get; set; } = "";
    public string Reason { get; set; } = "";
    public string? Subject { get; set; }
    public string? FromHeader { get; set; }
    public string? MessageDate { get; set; }
    public string? Detail { get; set; }
    public string LoggedAt { get; set; } = "";
}
