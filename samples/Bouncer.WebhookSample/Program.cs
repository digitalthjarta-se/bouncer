using Bouncer.Core.Webhooks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<WebhookReceiptLog>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/hooks/bounces", (
    HttpContext context,
    BouncePayload payload,
    WebhookReceiptLog receiptLog,
    ILogger<Program> logger) =>
{
    var expectedToken = app.Configuration["Webhook:BearerToken"];
    if (!string.IsNullOrEmpty(expectedToken) &&
        !string.Equals(
            context.Request.Headers.Authorization,
            $"Bearer {expectedToken}",
            StringComparison.Ordinal))
    {
        logger.LogWarning("Rejected webhook request with invalid bearer token");
        return Results.Unauthorized();
    }

    var receipt = receiptLog.Add(payload);
    logger.LogInformation(
        "Received webhook batch {ReceiptId} for route {Route} with {FailureCount} failure(s)",
        receipt.Id,
        payload.Route,
        payload.Failures.Count);

    foreach (var failure in payload.Failures)
    {
        logger.LogInformation(
            "Bounced address: {Email} (from: {OriginalFrom}, status: {StatusCode})",
            failure.Email,
            failure.OriginalFrom ?? "unknown",
            failure.StatusCode ?? "unknown");
    }

    return Results.Ok(new { receipt.Id });
});

app.MapGet("/api/receipts", (WebhookReceiptLog receiptLog) => receiptLog.GetAll());

app.MapDelete("/api/receipts", (WebhookReceiptLog receiptLog) =>
{
    receiptLog.Clear();
    return Results.NoContent();
});

app.Run();

public sealed record WebhookReceipt(
    long Id,
    DateTimeOffset ReceivedAtUtc,
    BouncePayload Payload);

public sealed class WebhookReceiptLog
{
    private const int MaxReceipts = 100;
    private readonly Lock gate = new();
    private readonly Queue<WebhookReceipt> receipts = new();
    private long nextId;

    public WebhookReceipt Add(BouncePayload payload)
    {
        lock (gate)
        {
            var receipt = new WebhookReceipt(++nextId, DateTimeOffset.UtcNow, payload);
            receipts.Enqueue(receipt);

            while (receipts.Count > MaxReceipts)
                receipts.Dequeue();

            return receipt;
        }
    }

    public IReadOnlyList<WebhookReceipt> GetAll()
    {
        lock (gate)
            return receipts.Reverse().ToArray();
    }

    public void Clear()
    {
        lock (gate)
            receipts.Clear();
    }
}
