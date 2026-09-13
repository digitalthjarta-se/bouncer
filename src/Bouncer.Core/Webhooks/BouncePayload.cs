namespace Bouncer.Core.Webhooks;

/// <summary>JSON body posted to a sender's webhook for one batch of failures on one route.</summary>
public sealed record BouncePayload(string Route, IReadOnlyList<BouncePayloadItem> Failures);

public sealed record BouncePayloadItem(
    string? OriginalFrom,
    string Email,
    string? OriginalRecipient,
    string? Action,
    string? StatusCode,
    string? DiagnosticCode,
    string? RemoteMta,
    string BouncedAtUtc);
