namespace Bouncer.Core.Configuration;

public enum Pop3Security
{
    SslOnConnect,
    StartTls,
    None,
}

public sealed class Pop3Options
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 995;
    public Pop3Security Security { get; set; } = Pop3Security.SslOnConnect;
    public string Username { get; set; } = "";
    public string? Password { get; set; }
    public string? PasswordEnvVar { get; set; }
    public int ConnectTimeoutSeconds { get; set; } = 30;

    public string ResolvePassword() =>
        SecretResolver.Resolve(Password, PasswordEnvVar)
        ?? throw new InvalidOperationException(
            "Pop3 password is not configured: set Bouncer:Pop3:Password or Bouncer:Pop3:PasswordEnvVar.");
}
