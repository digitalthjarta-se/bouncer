namespace Bouncer.Core.Data.Models;

public sealed class SenderBackoffRow
{
    public string WebhookRoute { get; set; } = "";
    public int FailureCount { get; set; }
    public string? NextRetryAt { get; set; }
    public string? LastError { get; set; }
    public string UpdatedAt { get; set; } = "";
}
