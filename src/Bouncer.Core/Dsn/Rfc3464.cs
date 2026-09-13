namespace Bouncer.Core.Dsn;

public static class Rfc3464
{
    /// <summary>
    /// Strips the "address-type;" prefix used by DSN fields such as Final-Recipient / Original-Recipient /
    /// Remote-MTA, e.g. "rfc822; user@example.com" -&gt; "user@example.com".
    /// </summary>
    public static string? StripAddressType(string? field)
    {
        if (string.IsNullOrWhiteSpace(field))
            return null;

        var semicolon = field.IndexOf(';');
        var value = semicolon >= 0 ? field[(semicolon + 1)..] : field;
        return value.Trim();
    }
}
