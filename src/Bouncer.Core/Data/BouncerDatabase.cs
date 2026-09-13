using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using Bouncer.Core.Configuration;

namespace Bouncer.Core.Data;

public sealed class BouncerDatabase
{
    private readonly string _connectionString;

    public BouncerDatabase(IOptions<BouncerOptions> options)
    {
        var dbPath = options.Value.Database.Path;
        var directory = Path.GetDirectoryName(Path.GetFullPath(dbPath));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        _connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
    }

    public SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    public void EnsureSchema()
    {
        using var connection = CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = ReadEmbeddedSchema();
        command.ExecuteNonQuery();
    }

    private static string ReadEmbeddedSchema()
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "Bouncer.Core.Data.Schema.sql";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
