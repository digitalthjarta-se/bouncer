# Bouncer

Monitors a shared POP3 bounce mailbox used by multiple applications (each sending under its
own `From` address), parses RFC 3464 delivery-status (DSN) bounce messages, and POSTs batches
of failed recipient addresses to a per-sender webhook. Once a batch is confirmed received by a
webhook, the underlying mail is deleted from the mailbox — the goal is to reach inbox zero
every day.

## How it works

Every `PollIntervalSeconds`, the worker runs a 3-phase cycle:

1. **Ingest** — connects to the POP3 mailbox, fetches any message not already fully processed,
   and classifies it:
   - A standard DSN (`multipart/report; report-type=delivery-status`) → one bounce record per
     failed recipient, resolved to a webhook route by the original `From` address.
   - Anything else (spam, replies, non-standard bounces) → logged to a local audit table and
     deleted immediately (this is a dedicated bounce mailbox — unrecognized mail is noise).
   - A message that fails to fetch/parse is retried up to `ParseFailure:MaxAttempts` times
     before being treated as junk.
2. **Dispatch** — batches all pending bounce records per webhook route and POSTs them as JSON.
   A successful (2xx) response marks the batch `reported`; a failure schedules an exponential
   backoff retry for that route without blocking other routes.
3. **Cleanup** — deletes from the mailbox any message whose bounce records are now fully
   resolved (`reported` or `dropped_unrouted`).

State (which messages have been seen, parsed bounce records, per-route backoff, and an audit
log of anything deleted without being reported) is kept in a local SQLite database, so restarts
never cause reprocessing or double-reporting.

## Configuration

Copy `appsettings.example.json` values into `src/Bouncer.Worker/appsettings.json` (or an
environment-specific `appsettings.<Environment>.json`), or override any value via environment
variables using the standard ASP.NET Core config convention, e.g. `Bouncer__Pop3__Host`.

Secrets (POP3 password, webhook bearer tokens) can be set inline in the config, but the
preferred approach is to leave them `null` and set the corresponding `*EnvVar` field to the
name of an environment variable holding the real value — see `PasswordEnvVar` /
`BearerTokenEnvVar` in `appsettings.example.json`.

Key settings:

| Setting | Meaning |
|---|---|
| `PollIntervalSeconds` | How often to poll the mailbox and dispatch webhooks. |
| `Pop3.*` | Mailbox connection details. `Security` is `SslOnConnect`, `StartTls`, or `None`. |
| `Webhooks.Routes[]` | One entry per app: `From` address → webhook `Url` (+ optional bearer token). Matching is exact and case-insensitive. |
| `Webhooks.DefaultRoute` | Catch-all webhook for bounces whose `From` doesn't match any route. If disabled, unmatched bounces are dropped (audited, then deleted — they can never be reported). |
| `Retry.*` | Exponential backoff (capped) applied per webhook route after a failed POST. |
| `ParseFailure.MaxAttempts` | How many times to retry fetching/parsing a message before treating it as junk. |

Config is validated at startup (`ValidateOnStart`) — the process fails fast with a clear error
if required fields are missing or invalid, rather than misbehaving at runtime.

See [`docs/WEBHOOKS.md`](docs/WEBHOOKS.md) for the receiving-endpoint contract: request/response
shape, payload schema, and retry/idempotency semantics for anything you point a `Webhooks.Routes[]`
or `DefaultRoute` URL at.

## Running

```bash
dotnet run --project src/Bouncer.Worker
```

Set `BOUNCER_POP3_PASSWORD` (and any webhook token env vars referenced in config) before
running.

## Deployment

- **systemd**: see `deploy/bouncer.service` + `deploy/bouncer.env.example`. Publish with
  `dotnet publish src/Bouncer.Worker -c Release -r linux-x64 --self-contained false -o /opt/bouncer`.
- **Docker**: `docker build -f deploy/Dockerfile -t bouncer .` — mount a volume at `/data` for
  the SQLite database and inject secrets via `-e`/an env file.

## Testing

```bash
dotnet test
```

`Bouncer.Tests` covers DSN parsing (against real `.eml` fixtures in `Fixtures/`, including
multi-recipient bounces, the `text/rfc822-headers` variant, missing original-message DSNs,
non-bounce mail, and a malformed/truncated message), backoff math, webhook route resolution,
webhook POST handling (via a fake `HttpMessageHandler`), and repository behavior against a
temp-file SQLite database.

A full end-to-end run (real POP3 mailbox + a test webhook receiver) should be verified manually
before go-live — that's not something worth automating for this project's size.
