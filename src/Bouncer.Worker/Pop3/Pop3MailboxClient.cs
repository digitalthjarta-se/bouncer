using Bouncer.Core.Configuration;
using MailKit.Net.Pop3;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Bouncer.Worker.Pop3;

/// <summary>Thin wrapper around MailKit's <see cref="Pop3Client"/> for one POP3 session.</summary>
public sealed class Pop3MailboxClient(IOptions<BouncerOptions> options) : IAsyncDisposable
{
    private readonly Pop3Options _options = options.Value.Pop3;
    private readonly Pop3Client _client = new();

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.ConnectTimeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var security = _options.Security switch
        {
            Pop3Security.SslOnConnect => SecureSocketOptions.SslOnConnect,
            Pop3Security.StartTls => SecureSocketOptions.StartTls,
            _ => SecureSocketOptions.None,
        };

        await _client.ConnectAsync(_options.Host, _options.Port, security, linked.Token);
        await _client.AuthenticateAsync(_options.Username, _options.ResolvePassword(), cancellationToken);
    }

    public Task<IList<string>> GetMessageUidsAsync(CancellationToken cancellationToken) =>
        _client.GetMessageUidsAsync(cancellationToken);

    public Task<MimeMessage> GetMessageAsync(int index, CancellationToken cancellationToken) =>
        _client.GetMessageAsync(index, cancellationToken);

    public Task DeleteMessageAsync(int index, CancellationToken cancellationToken) =>
        _client.DeleteMessageAsync(index, cancellationToken);

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        if (_client.IsConnected)
            await _client.DisconnectAsync(quit: true, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_client.IsConnected)
            await _client.DisconnectAsync(quit: false);
        _client.Dispose();
    }
}
