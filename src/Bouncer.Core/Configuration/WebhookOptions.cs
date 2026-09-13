namespace Bouncer.Core.Configuration;

public sealed class WebhookOptions
{
    public int TimeoutSeconds { get; set; } = 15;
    public int MaxBatchSize { get; set; } = 200;
    public DefaultWebhookRoute DefaultRoute { get; set; } = new();
    public List<WebhookRoute> Routes { get; set; } = [];
}
