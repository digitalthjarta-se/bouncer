namespace Bouncer.Core.Dsn;

public enum DsnKind
{
    Bounce,
    NonBounce,
}

public sealed record DsnClassification(DsnKind Kind, string? Reason, IReadOnlyList<ParsedBounce> Bounces)
{
    public static DsnClassification NonBounce(string reason) => new(DsnKind.NonBounce, reason, []);

    public static DsnClassification Bounce(IReadOnlyList<ParsedBounce> bounces) => new(DsnKind.Bounce, null, bounces);
}
