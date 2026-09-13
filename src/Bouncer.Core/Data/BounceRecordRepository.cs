using Dapper;
using Bouncer.Core.Data.Models;

namespace Bouncer.Core.Data;

public sealed class BounceRecordRepository(BouncerDatabase database)
{
    public void Insert(BounceRecordRow row)
    {
        using var connection = database.CreateConnection();
        connection.Execute(
            """
            INSERT INTO bounce_records
                (uidl, original_from, final_recipient, original_recipient, action, status_code,
                 diagnostic_code, remote_mta, reporting_mta, arrival_date, bounce_timestamp,
                 webhook_route, status, created_at, reported_at)
            VALUES
                (@Uidl, @OriginalFrom, @FinalRecipient, @OriginalRecipient, @Action, @StatusCode,
                 @DiagnosticCode, @RemoteMta, @ReportingMta, @ArrivalDate, @BounceTimestamp,
                 @WebhookRoute, @Status, @CreatedAt, @ReportedAt)
            """,
            row);
    }

    public IReadOnlyList<string> GetPendingRoutes()
    {
        using var connection = database.CreateConnection();
        return connection.Query<string>(
            "SELECT DISTINCT webhook_route FROM bounce_records WHERE status = @Status",
            new { Status = BounceRecordStatus.Pending }).AsList();
    }

    public IReadOnlyList<BounceRecordRow> GetPendingBatch(string webhookRoute, int maxBatchSize)
    {
        using var connection = database.CreateConnection();
        return connection.Query<BounceRecordRow>(
            """
            SELECT id AS Id, uidl AS Uidl, original_from AS OriginalFrom, final_recipient AS FinalRecipient,
                   original_recipient AS OriginalRecipient, action AS Action, status_code AS StatusCode,
                   diagnostic_code AS DiagnosticCode, remote_mta AS RemoteMta, reporting_mta AS ReportingMta,
                   arrival_date AS ArrivalDate, bounce_timestamp AS BounceTimestamp, webhook_route AS WebhookRoute,
                   status AS Status, created_at AS CreatedAt, reported_at AS ReportedAt
            FROM bounce_records
            WHERE webhook_route = @WebhookRoute AND status = @Status
            ORDER BY id
            LIMIT @MaxBatchSize
            """,
            new { WebhookRoute = webhookRoute, Status = BounceRecordStatus.Pending, MaxBatchSize = maxBatchSize }).AsList();
    }

    public void MarkReported(IEnumerable<long> ids, DateTime reportedAtUtc)
    {
        var idList = ids as IReadOnlyCollection<long> ?? ids.ToList();
        if (idList.Count == 0) return;

        using var connection = database.CreateConnection();
        connection.Execute(
            "UPDATE bounce_records SET status = @Status, reported_at = @ReportedAt WHERE id IN @Ids",
            new { Status = BounceRecordStatus.Reported, ReportedAt = reportedAtUtc.ToString("o"), Ids = idList });
    }
}
