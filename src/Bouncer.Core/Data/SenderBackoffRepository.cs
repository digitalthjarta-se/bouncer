using Dapper;
using Bouncer.Core.Data.Models;

namespace Bouncer.Core.Data;

public sealed class SenderBackoffRepository(BouncerDatabase database)
{
    public SenderBackoffRow? Find(string webhookRoute)
    {
        using var connection = database.CreateConnection();
        return connection.QuerySingleOrDefault<SenderBackoffRow>(
            """
            SELECT webhook_route AS WebhookRoute, failure_count AS FailureCount,
                   next_retry_at AS NextRetryAt, last_error AS LastError, updated_at AS UpdatedAt
            FROM sender_backoff WHERE webhook_route = @WebhookRoute
            """,
            new { WebhookRoute = webhookRoute });
    }

    public void RecordFailure(string webhookRoute, int failureCount, DateTime nextRetryAtUtc, string? lastError, DateTime nowUtc)
    {
        using var connection = database.CreateConnection();
        connection.Execute(
            """
            INSERT INTO sender_backoff (webhook_route, failure_count, next_retry_at, last_error, updated_at)
            VALUES (@WebhookRoute, @FailureCount, @NextRetryAt, @LastError, @UpdatedAt)
            ON CONFLICT(webhook_route) DO UPDATE SET
                failure_count = @FailureCount,
                next_retry_at = @NextRetryAt,
                last_error = @LastError,
                updated_at = @UpdatedAt
            """,
            new
            {
                WebhookRoute = webhookRoute,
                FailureCount = failureCount,
                NextRetryAt = nextRetryAtUtc.ToString("o"),
                LastError = lastError,
                UpdatedAt = nowUtc.ToString("o"),
            });
    }

    public void Reset(string webhookRoute, DateTime nowUtc)
    {
        using var connection = database.CreateConnection();
        connection.Execute(
            """
            INSERT INTO sender_backoff (webhook_route, failure_count, next_retry_at, last_error, updated_at)
            VALUES (@WebhookRoute, 0, NULL, NULL, @UpdatedAt)
            ON CONFLICT(webhook_route) DO UPDATE SET
                failure_count = 0,
                next_retry_at = NULL,
                last_error = NULL,
                updated_at = @UpdatedAt
            """,
            new { WebhookRoute = webhookRoute, UpdatedAt = nowUtc.ToString("o") });
    }
}
