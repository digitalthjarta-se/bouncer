namespace Bouncer.Core.Dsn;

public sealed record ParsedBounce(
    string? OriginalFrom,
    string FinalRecipient,
    string? OriginalRecipient,
    string? Action,
    string? StatusCode,
    string? DiagnosticCode,
    string? RemoteMta,
    string? ReportingMta,
    string? ArrivalDate);
