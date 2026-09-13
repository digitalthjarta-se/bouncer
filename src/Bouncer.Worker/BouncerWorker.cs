using System.Globalization;
using Bouncer.Core.Configuration;
using Bouncer.Core.Data;
using Bouncer.Core.Data.Models;
using Bouncer.Core.Dsn;
using Bouncer.Core.Retry;
using Bouncer.Core.Routing;
using Bouncer.Core.Webhooks;
using Bouncer.Worker.Pop3;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Bouncer.Worker;

public sealed class BouncerWorker(
    ILogger<BouncerWorker> logger,
    IOptions<BouncerOptions> options,
    ProcessedMessageRepository processedMessages,
    BounceRecordRepository bounceRecords,
    SenderBackoffRepository senderBackoff,
    AuditLogRepository auditLog,
    WebhookSender webhookSender) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.PollIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Poll cycle failed; will retry next interval");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        var cycleId = Guid.NewGuid();
        using var scope = logger.BeginScope(new Dictionary<string, object> { ["CycleId"] = cycleId });

        await IngestNewMessagesAsync(cancellationToken);
        await DispatchWebhooksAsync(cancellationToken);
        await CleanupResolvedMessagesAsync(cancellationToken);
    }

    private async Task IngestNewMessagesAsync(CancellationToken cancellationToken)
    {
        await using var client = new Pop3MailboxClient(options);
        await client.ConnectAsync(cancellationToken);

        var deleteIndexes = new List<int>();
        try
        {
            var uidls = await client.GetMessageUidsAsync(cancellationToken);
            var resolved = processedMessages.GetResolvedUidls();
            var now = DateTime.UtcNow;

            for (var index = 0; index < uidls.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var uidl = uidls[index];
                if (resolved.Contains(uidl))
                    continue;

                processedMessages.EnsureSeen(uidl, messageId: null, now);

                MimeMessage message;
                try
                {
                    message = await client.GetMessageAsync(index, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    var attempts = processedMessages.IncrementParseAttempts(uidl);
                    logger.LogWarning(ex, "Failed to fetch/parse message {Uidl} (attempt {Attempts})", uidl, attempts);

                    if (attempts >= options.Value.ParseFailure.MaxAttempts)
                    {
                        auditLog.Insert(new AuditLogRow
                        {
                            Uidl = uidl,
                            Reason = AuditReason.ParseErrorExhausted,
                            Detail = ex.Message,
                            LoggedAt = DateTime.UtcNow.ToString("o"),
                        });
                        processedMessages.MarkClassification(uidl, MessageClassification.ParseError);
                        deleteIndexes.Add(index);
                    }
                    continue;
                }

                ClassifyAndPersist(uidl, index, message, deleteIndexes);
            }

            foreach (var index in deleteIndexes)
                await client.DeleteMessageAsync(index, cancellationToken);
        }
        finally
        {
            await client.DisconnectAsync(cancellationToken);
        }
    }

    private void ClassifyAndPersist(string uidl, int index, MimeMessage message, List<int> deleteIndexes)
    {
        var now = DateTime.UtcNow;
        var classification = DsnParser.Classify(message);

        if (classification.Kind == DsnKind.NonBounce)
        {
            auditLog.Insert(new AuditLogRow
            {
                Uidl = uidl,
                Reason = AuditReason.NonBounce,
                Subject = message.Subject,
                FromHeader = message.From.ToString(),
                MessageDate = message.Date.ToString("o"),
                Detail = classification.Reason,
                LoggedAt = now.ToString("o"),
            });
            processedMessages.MarkClassification(uidl, MessageClassification.NonBounce, message.MessageId);
            deleteIndexes.Add(index);
            return;
        }

        var allDropped = true;
        foreach (var bounce in classification.Bounces)
        {
            var route = WebhookRouteResolver.Resolve(bounce.OriginalFrom, options.Value.Webhooks);
            var status = route.Kind == WebhookRouteKind.Unrouted ? BounceRecordStatus.DroppedUnrouted : BounceRecordStatus.Pending;
            if (status != BounceRecordStatus.DroppedUnrouted)
                allDropped = false;

            bounceRecords.Insert(new BounceRecordRow
            {
                Uidl = uidl,
                OriginalFrom = bounce.OriginalFrom,
                FinalRecipient = bounce.FinalRecipient,
                OriginalRecipient = bounce.OriginalRecipient,
                Action = bounce.Action,
                StatusCode = bounce.StatusCode,
                DiagnosticCode = bounce.DiagnosticCode,
                RemoteMta = bounce.RemoteMta,
                ReportingMta = bounce.ReportingMta,
                ArrivalDate = bounce.ArrivalDate,
                BounceTimestamp = now.ToString("o"),
                WebhookRoute = route.RouteKey,
                Status = status,
                CreatedAt = now.ToString("o"),
            });

            if (status == BounceRecordStatus.DroppedUnrouted)
            {
                auditLog.Insert(new AuditLogRow
                {
                    Uidl = uidl,
                    Reason = AuditReason.DroppedUnrouted,
                    Subject = message.Subject,
                    FromHeader = bounce.OriginalFrom,
                    MessageDate = message.Date.ToString("o"),
                    Detail = $"No webhook route for '{bounce.OriginalFrom ?? "(unknown)"}' and no default route configured.",
                    LoggedAt = now.ToString("o"),
                });
            }
        }

        processedMessages.MarkClassification(uidl, MessageClassification.Bounce, message.MessageId);
        if (allDropped)
            deleteIndexes.Add(index);
    }

    private async Task DispatchWebhooksAsync(CancellationToken cancellationToken)
    {
        var routes = bounceRecords.GetPendingRoutes();
        foreach (var route in routes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var backoff = senderBackoff.Find(route);
            if (IsBackingOff(backoff))
                continue;

            var (url, bearerToken) = ResolveRouteTarget(route);
            if (url is null)
            {
                logger.LogWarning("No URL configured for webhook route '{Route}'; leaving batch pending", route);
                continue;
            }

            var batch = bounceRecords.GetPendingBatch(route, options.Value.Webhooks.MaxBatchSize);
            if (batch.Count == 0)
                continue;

            var payload = new BouncePayload(route, batch.Select(ToPayloadItem).ToList());
            var result = await webhookSender.PostAsync(
                url, bearerToken, payload, TimeSpan.FromSeconds(options.Value.Webhooks.TimeoutSeconds), cancellationToken);

            var now = DateTime.UtcNow;
            if (result.Success)
            {
                bounceRecords.MarkReported(batch.Select(r => r.Id), now);
                senderBackoff.Reset(route, now);
                logger.LogInformation("Reported {Count} failure(s) for route '{Route}'", batch.Count, route);
            }
            else
            {
                var failureCount = (backoff?.FailureCount ?? 0) + 1;
                var delay = BackoffCalculator.Compute(failureCount, options.Value.Retry);
                senderBackoff.RecordFailure(route, failureCount, now + delay, result.Error, now);
                logger.LogWarning(
                    "Webhook POST failed for route '{Route}': {Error}. Next retry in {Delay}", route, result.Error, delay);
            }
        }
    }

    private static bool IsBackingOff(SenderBackoffRow? backoff)
    {
        if (backoff?.NextRetryAt is not { } raw)
            return false;

        return DateTime.Parse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) > DateTime.UtcNow;
    }

    private (string? Url, string? BearerToken) ResolveRouteTarget(string route)
    {
        if (route == WebhookRouteResolver.DefaultRouteKey)
        {
            var defaultRoute = options.Value.Webhooks.DefaultRoute;
            return (defaultRoute.Url, defaultRoute.ResolveBearerToken());
        }

        var match = options.Value.Webhooks.Routes.FirstOrDefault(r => string.Equals(r.From, route, StringComparison.OrdinalIgnoreCase));
        return match is null ? (null, null) : (match.Url, match.ResolveBearerToken());
    }

    private static BouncePayloadItem ToPayloadItem(BounceRecordRow row) => new(
        row.OriginalFrom,
        row.FinalRecipient,
        row.OriginalRecipient,
        row.Action,
        row.StatusCode,
        row.DiagnosticCode,
        row.RemoteMta,
        row.BounceTimestamp);

    private async Task CleanupResolvedMessagesAsync(CancellationToken cancellationToken)
    {
        var readyUidls = processedMessages.GetUidlsReadyForCleanup();
        if (readyUidls.Count == 0)
            return;

        var readySet = new HashSet<string>(readyUidls, StringComparer.Ordinal);

        await using var client = new Pop3MailboxClient(options);
        await client.ConnectAsync(cancellationToken);
        try
        {
            var uidls = await client.GetMessageUidsAsync(cancellationToken);
            var now = DateTime.UtcNow;

            for (var index = 0; index < uidls.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var uidl = uidls[index];
                if (!readySet.Contains(uidl))
                    continue;

                await client.DeleteMessageAsync(index, cancellationToken);
                processedMessages.MarkDeleted(uidl, now);
            }
        }
        finally
        {
            await client.DisconnectAsync(cancellationToken);
        }
    }
}
