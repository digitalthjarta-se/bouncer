namespace Bouncer.Core.Data.Models;

public static class BounceRecordStatus
{
    public const string Pending = "pending";
    public const string Reported = "reported";
    public const string DroppedUnrouted = "dropped_unrouted";
}

public sealed class BounceRecordRow
{
    public long Id { get; set; }
    public string Uidl { get; set; } = "";
    public string? OriginalFrom { get; set; }
    public string FinalRecipient { get; set; } = "";
    public string? OriginalRecipient { get; set; }
    public string? Action { get; set; }
    public string? StatusCode { get; set; }
    public string? DiagnosticCode { get; set; }
    public string? RemoteMta { get; set; }
    public string? ReportingMta { get; set; }
    public string? ArrivalDate { get; set; }
    public string BounceTimestamp { get; set; } = "";
    public string WebhookRoute { get; set; } = "";
    public string Status { get; set; } = BounceRecordStatus.Pending;
    public string CreatedAt { get; set; } = "";
    public string? ReportedAt { get; set; }
}
