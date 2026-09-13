using Dapper;
using Bouncer.Core.Data.Models;

namespace Bouncer.Core.Data;

public sealed class ProcessedMessageRepository(BouncerDatabase database)
{
    /// <summary>Uidls that have already been fully classified (bounce, non_bounce, or parse_error) and never need reprocessing.</summary>
    public HashSet<string> GetResolvedUidls()
    {
        using var connection = database.CreateConnection();
        var uidls = connection.Query<string>(
            "SELECT uidl FROM processed_messages WHERE classification != @Unclassified",
            new { Unclassified = MessageClassification.Unclassified });
        return new HashSet<string>(uidls, StringComparer.Ordinal);
    }

    public void EnsureSeen(string uidl, string? messageId, DateTime nowUtc)
    {
        using var connection = database.CreateConnection();
        connection.Execute(
            """
            INSERT INTO processed_messages (uidl, message_id, first_seen_at, classification, parse_attempts, deleted_from_mailbox)
            VALUES (@Uidl, @MessageId, @FirstSeenAt, 'unclassified', 0, 0)
            ON CONFLICT(uidl) DO NOTHING
            """,
            new { Uidl = uidl, MessageId = messageId, FirstSeenAt = nowUtc.ToString("o") });
    }

    public int IncrementParseAttempts(string uidl)
    {
        using var connection = database.CreateConnection();
        connection.Execute(
            "UPDATE processed_messages SET parse_attempts = parse_attempts + 1 WHERE uidl = @Uidl",
            new { Uidl = uidl });
        return connection.QuerySingle<int>(
            "SELECT parse_attempts FROM processed_messages WHERE uidl = @Uidl", new { Uidl = uidl });
    }

    public void MarkClassification(string uidl, string classification, string? messageId = null)
    {
        using var connection = database.CreateConnection();
        connection.Execute(
            "UPDATE processed_messages SET classification = @Classification, message_id = COALESCE(@MessageId, message_id) WHERE uidl = @Uidl",
            new { Uidl = uidl, Classification = classification, MessageId = messageId });
    }

    public void MarkDeleted(string uidl, DateTime deletedAtUtc)
    {
        using var connection = database.CreateConnection();
        connection.Execute(
            "UPDATE processed_messages SET deleted_from_mailbox = 1, deleted_at = @DeletedAt WHERE uidl = @Uidl",
            new { Uidl = uidl, DeletedAt = deletedAtUtc.ToString("o") });
    }

    /// <summary>Bounce messages whose child bounce_records are all resolved (reported or dropped_unrouted) and are safe to delete.</summary>
    public IReadOnlyList<string> GetUidlsReadyForCleanup()
    {
        using var connection = database.CreateConnection();
        return connection.Query<string>(
            """
            SELECT uidl FROM processed_messages
            WHERE classification = 'bounce' AND deleted_from_mailbox = 0
              AND uidl NOT IN (SELECT uidl FROM bounce_records WHERE status = 'pending')
            """).AsList();
    }
}
