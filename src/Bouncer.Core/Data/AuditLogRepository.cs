using Dapper;
using Bouncer.Core.Data.Models;

namespace Bouncer.Core.Data;

public sealed class AuditLogRepository(BouncerDatabase database)
{
    public void Insert(AuditLogRow row)
    {
        using var connection = database.CreateConnection();
        connection.Execute(
            """
            INSERT INTO audit_log (uidl, reason, subject, from_header, message_date, detail, logged_at)
            VALUES (@Uidl, @Reason, @Subject, @FromHeader, @MessageDate, @Detail, @LoggedAt)
            """,
            row);
    }
}
