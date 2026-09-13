using System.Text;
using MimeKit;

namespace Bouncer.Core.Dsn;

/// <summary>
/// Classifies an inbound message as an RFC 3464 delivery-status bounce or as non-bounce mail,
/// extracting one <see cref="ParsedBounce"/> per failed recipient when it is a bounce.
/// </summary>
public static class DsnParser
{
    public static DsnClassification Classify(MimeMessage message)
    {
        if (message.Body is not MultipartReport report ||
            !string.Equals(report.ReportType, "delivery-status", StringComparison.OrdinalIgnoreCase))
        {
            return DsnClassification.NonBounce("not_multipart_report_delivery_status");
        }

        var deliveryStatus = report.OfType<MessageDeliveryStatus>().FirstOrDefault();
        if (deliveryStatus is null)
            return DsnClassification.NonBounce("missing_delivery_status_part");

        var groups = deliveryStatus.StatusGroups;
        if (groups.Count == 0)
            return DsnClassification.NonBounce("empty_delivery_status");

        var perMessageFields = groups[0];
        var reportingMta = Rfc3464.StripAddressType(perMessageFields["Reporting-MTA"]);
        var arrivalDate = perMessageFields["Arrival-Date"];

        var originalFrom = ExtractOriginalFrom(report);

        var bounces = new List<ParsedBounce>();
        foreach (var group in groups.Skip(1))
        {
            var finalRecipientRaw = group["Final-Recipient"];
            if (string.IsNullOrEmpty(finalRecipientRaw))
                continue;

            var finalRecipient = Rfc3464.StripAddressType(finalRecipientRaw);
            if (string.IsNullOrEmpty(finalRecipient))
                continue;

            bounces.Add(new ParsedBounce(
                OriginalFrom: originalFrom?.ToLowerInvariant(),
                FinalRecipient: finalRecipient,
                OriginalRecipient: Rfc3464.StripAddressType(group["Original-Recipient"]),
                Action: group["Action"],
                StatusCode: group["Status"],
                DiagnosticCode: group["Diagnostic-Code"],
                RemoteMta: Rfc3464.StripAddressType(group["Remote-MTA"]),
                ReportingMta: reportingMta,
                ArrivalDate: arrivalDate));
        }

        return bounces.Count == 0
            ? DsnClassification.NonBounce("no_recipient_groups")
            : DsnClassification.Bounce(bounces);
    }

    private static string? ExtractOriginalFrom(MultipartReport report)
    {
        foreach (var part in report)
        {
            if (part is MessagePart { Message.From.Mailboxes: var mailboxes } &&
                mailboxes.FirstOrDefault() is { } mailbox)
            {
                return mailbox.Address;
            }

            if (part is TextPart text &&
                string.Equals(text.ContentType.MediaSubtype, "rfc822-headers", StringComparison.OrdinalIgnoreCase))
            {
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text.Text));
                var headers = HeaderList.Load(stream);
                if (headers["From"] is string raw && MailboxAddress.TryParse(raw, out var parsed))
                    return parsed.Address;
            }
        }

        return null;
    }
}
