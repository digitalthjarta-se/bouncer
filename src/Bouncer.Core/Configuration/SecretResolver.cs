namespace Bouncer.Core.Configuration;

/// <summary>
/// Resolves a config value that may be given either as a literal or via an environment
/// variable name, so secrets never need to be committed to the config file.
/// </summary>
public static class SecretResolver
{
    public static string? Resolve(string? literalValue, string? envVarName)
    {
        if (!string.IsNullOrEmpty(literalValue))
            return literalValue;

        return string.IsNullOrEmpty(envVarName) ? null : Environment.GetEnvironmentVariable(envVarName);
    }
}
