# AGENTS.md

## Project overview

Bouncer is a .NET 10 background worker for a shared POP3 bounce mailbox. It parses RFC 3464
delivery-status notifications (DSNs), stores processing state in SQLite, groups failed
recipients by the original sender, and delivers batches to sender-specific webhooks.

The worker is designed for at-least-once webhook delivery and safe restarts. Mail is deleted
only after its bounce records are resolved, except for messages deliberately classified as
noise, exhausted parse failures, or unrouted bounces.

## Repository layout

- `Bouncer.slnx` — solution containing all production and test projects.
- `src/Bouncer.Core/` — domain and infrastructure logic with no worker-host dependency.
  - `Configuration/` — strongly typed options, startup validation, and secret resolution.
  - `Data/` — SQLite setup, embedded `Schema.sql`, repositories, and persistence models.
  - `Dsn/` — MimeKit-based DSN classification and RFC 3464 parsing.
  - `Retry/` — exponential backoff calculation.
  - `Routing/` — original-sender to webhook-route resolution.
  - `Webhooks/` — payload DTOs and HTTP delivery.
- `src/Bouncer.Worker/` — executable .NET Worker Service.
  - `Program.cs` wires configuration, repositories, HTTP, and the hosted service.
  - `BouncerWorker.cs` runs the ingest, dispatch, and cleanup cycle.
  - `Pop3/` contains the MailKit POP3 adapter.
- `tests/Bouncer.Tests/` — xUnit tests plus realistic `.eml` fixtures under `Fixtures/`.
- `docs/WEBHOOKS.md` — receiver-facing webhook contract and delivery semantics.
- `deploy/` — Docker and systemd deployment assets.
- `appsettings.example.json` — complete configuration example.

Generated `bin/` and `obj/` directories are not source and should not be edited.

## Prerequisites and common commands

Install the .NET 10 SDK. Run commands from the repository root:

```bash
dotnet restore
dotnet build Bouncer.slnx
dotnet test
dotnet run --project src/Bouncer.Worker
```

For a focused test:

```bash
dotnet test --filter FullyQualifiedName~DsnParserTests
```

For deployment:

```bash
dotnet publish src/Bouncer.Worker -c Release -r linux-x64 \
  --self-contained false -o /opt/bouncer
docker build -f deploy/Dockerfile -t bouncer .
```

## Local configuration

Configuration is rooted at `Bouncer` and validated at startup. Start from
`appsettings.example.json`; place machine-specific overrides in an ignored
`appsettings.Local.json` or use standard .NET environment variables such as
`Bouncer__Pop3__Host`.

Do not commit mailbox passwords, webhook bearer tokens, local databases, or environment files.
Prefer the `PasswordEnvVar` and `BearerTokenEnvVar` settings, then set the referenced
environment variables in the runtime environment.

Running the worker contacts a real POP3 server and configured webhooks. Unit tests do not
require those external services.

## Processing model and invariants

Each poll cycle runs three phases in order:

1. **Ingest:** fetch unseen POP3 messages, classify DSNs, and persist bounce records.
2. **Dispatch:** send one bounded pending batch per route, independently respecting each
   route's retry backoff.
3. **Cleanup:** delete mailbox messages only when all associated records are resolved.

Preserve these behaviors when changing the code:

- POP3 UIDLs are the durable message identity; restarts must not duplicate ingestion.
- SQLite is the source of truth for processed messages, bounce records, audit entries, and
  per-route backoff.
- Webhook success means any 2xx response. Failures retry the entire batch, so delivery remains
  at least once and receivers must be able to handle duplicates.
- A failing webhook route must not block other routes.
- Route matching is exact and case-insensitive on the original `From` address.
- Unrecognized mailbox content is audited and deleted because this is a dedicated bounce
  mailbox.
- Cancellation must propagate through network and worker operations; do not swallow it as a
  normal failure.
- Schema changes must update the embedded `src/Bouncer.Core/Data/Schema.sql` and the
  repositories/models that consume it.
- Changes to `BouncePayload` are public contract changes and must also update
  `docs/WEBHOOKS.md`.

## Development conventions

- Nullable reference types and implicit usings are enabled.
- Keep reusable parsing, persistence, routing, retry, and webhook logic in `Bouncer.Core`;
  keep hosting and POP3-cycle orchestration in `Bouncer.Worker`.
- Follow the existing dependency-injection and options-validation patterns in `Program.cs`.
- Use async APIs and pass `CancellationToken` through I/O boundaries.
- Surface failures through the existing structured logging patterns rather than silently
  defaulting or catching broad exceptions.
- Add or update xUnit coverage with behavior changes. Use `.eml` fixtures for parser cases,
  temporary SQLite files for repository tests, and fake `HttpMessageHandler` instances for
  webhook tests.
- Keep changes narrowly scoped and preserve the mailbox deletion and retry guarantees above.

Read `README.md` for the operational overview and `docs/WEBHOOKS.md` before modifying delivery
behavior or payload semantics.
