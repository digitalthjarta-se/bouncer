using Bouncer.Core.Dsn;
using MimeKit;

namespace Bouncer.Tests;

public class DsnParserTests
{
    private static MimeMessage LoadFixture(string fileName) =>
        MimeMessage.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName));

    [Fact]
    public void Classify_StandardSingleRecipientDsn_ReturnsSingleBounceWithOriginalFrom()
    {
        var message = LoadFixture("dsn-standard-single-recipient.eml");

        var result = DsnParser.Classify(message);

        Assert.Equal(DsnKind.Bounce, result.Kind);
        var bounce = Assert.Single(result.Bounces);
        Assert.Equal("app1@example.com", bounce.OriginalFrom);
        Assert.Equal("nobody@nowhere.invalid", bounce.FinalRecipient);
        Assert.Equal("failed", bounce.Action);
        Assert.Equal("5.1.1", bounce.StatusCode);
        Assert.Equal("mx.nowhere.invalid", bounce.RemoteMta);
        Assert.Equal("mail.example.com", bounce.ReportingMta);
        Assert.Contains("550", bounce.DiagnosticCode);
    }

    [Fact]
    public void Classify_MultiRecipientDsn_ReturnsOneBouncePerRecipient()
    {
        var message = LoadFixture("dsn-multi-recipient.eml");

        var result = DsnParser.Classify(message);

        Assert.Equal(DsnKind.Bounce, result.Kind);
        Assert.Equal(2, result.Bounces.Count);
        Assert.All(result.Bounces, b => Assert.Equal("app2@example.com", b.OriginalFrom));
        Assert.Contains(result.Bounces, b => b.FinalRecipient == "first.bounce@nowhere.invalid" && b.StatusCode == "5.1.1");
        Assert.Contains(result.Bounces, b => b.FinalRecipient == "second.bounce@nowhere.invalid" && b.StatusCode == "5.2.2");
    }

    [Fact]
    public void Classify_TextRfc822HeadersVariant_ExtractsOriginalFrom()
    {
        var message = LoadFixture("dsn-text-rfc822-headers.eml");

        var result = DsnParser.Classify(message);

        Assert.Equal(DsnKind.Bounce, result.Kind);
        var bounce = Assert.Single(result.Bounces);
        Assert.Equal("app3@example.com", bounce.OriginalFrom);
        Assert.Equal("ghost@nowhere.invalid", bounce.FinalRecipient);
    }

    [Fact]
    public void Classify_MissingOriginalMessage_StillReturnsBounceWithNullOriginalFrom()
    {
        var message = LoadFixture("dsn-missing-original-message.eml");

        var result = DsnParser.Classify(message);

        Assert.Equal(DsnKind.Bounce, result.Kind);
        var bounce = Assert.Single(result.Bounces);
        Assert.Null(bounce.OriginalFrom);
        Assert.Equal("someone@nowhere.invalid", bounce.FinalRecipient);
    }

    [Fact]
    public void Classify_Spam_ReturnsNonBounce()
    {
        var message = LoadFixture("non-bounce-spam.eml");

        var result = DsnParser.Classify(message);

        Assert.Equal(DsnKind.NonBounce, result.Kind);
        Assert.Empty(result.Bounces);
    }

    [Fact]
    public void Classify_HumanReply_ReturnsNonBounce()
    {
        var message = LoadFixture("non-bounce-human-reply.eml");

        var result = DsnParser.Classify(message);

        Assert.Equal(DsnKind.NonBounce, result.Kind);
    }

    [Fact]
    public void Classify_TruncatedMalformedMessage_DoesNotThrowAndClassifiesAsNonBounce()
    {
        var message = LoadFixture("malformed-truncated.eml");

        var result = DsnParser.Classify(message);

        Assert.Equal(DsnKind.NonBounce, result.Kind);
    }
}
