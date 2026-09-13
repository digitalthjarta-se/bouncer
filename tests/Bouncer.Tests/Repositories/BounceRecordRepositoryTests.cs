using Bouncer.Core.Configuration;
using Bouncer.Core.Data;
using Bouncer.Core.Data.Models;
using Microsoft.Extensions.Options;

namespace Bouncer.Tests.Repositories;

public class BounceRecordRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly BouncerDatabase _database;

    public BounceRecordRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"bouncer-test-{Guid.NewGuid():N}.db");
        var options = Options.Create(new BouncerOptions { Database = new DatabaseOptions { Path = _dbPath } });
        _database = new BouncerDatabase(options);
        _database.EnsureSchema();
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    private static BounceRecordRow SampleRow(string uidl, string route = "app1@example.com") => new()
    {
        Uidl = uidl,
        OriginalFrom = "app1@example.com",
        FinalRecipient = "fail@nowhere.invalid",
        BounceTimestamp = DateTime.UtcNow.ToString("o"),
        WebhookRoute = route,
        Status = BounceRecordStatus.Pending,
        CreatedAt = DateTime.UtcNow.ToString("o"),
    };

    [Fact]
    public void InsertAndGetPendingBatch_RoundTrips()
    {
        var processedMessages = new ProcessedMessageRepository(_database);
        processedMessages.EnsureSeen("uidl-1", null, DateTime.UtcNow);

        var repo = new BounceRecordRepository(_database);
        repo.Insert(SampleRow("uidl-1"));

        var batch = repo.GetPendingBatch("app1@example.com", 10);

        var row = Assert.Single(batch);
        Assert.Equal("fail@nowhere.invalid", row.FinalRecipient);
        Assert.Equal(BounceRecordStatus.Pending, row.Status);
    }

    [Fact]
    public void MarkReported_ChangesStatusAndExcludesFromPendingBatch()
    {
        var processedMessages = new ProcessedMessageRepository(_database);
        processedMessages.EnsureSeen("uidl-2", null, DateTime.UtcNow);

        var repo = new BounceRecordRepository(_database);
        repo.Insert(SampleRow("uidl-2"));
        var inserted = Assert.Single(repo.GetPendingBatch("app1@example.com", 10));

        repo.MarkReported([inserted.Id], DateTime.UtcNow);

        Assert.Empty(repo.GetPendingBatch("app1@example.com", 10));
        Assert.DoesNotContain("app1@example.com", repo.GetPendingRoutes());
    }

    [Fact]
    public void GetPendingRoutes_ReturnsDistinctRoutesAcrossRecords()
    {
        var processedMessages = new ProcessedMessageRepository(_database);
        processedMessages.EnsureSeen("uidl-3", null, DateTime.UtcNow);
        processedMessages.EnsureSeen("uidl-4", null, DateTime.UtcNow);

        var repo = new BounceRecordRepository(_database);
        repo.Insert(SampleRow("uidl-3", route: "app1@example.com"));
        repo.Insert(SampleRow("uidl-4", route: "app2@example.com"));

        var routes = repo.GetPendingRoutes();

        Assert.Contains("app1@example.com", routes);
        Assert.Contains("app2@example.com", routes);
    }
}
