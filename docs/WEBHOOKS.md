# Webhook contract

This describes what your endpoint must accept if it's configured as a `Webhooks.Routes[]` or
`Webhooks.DefaultRoute` target in Bouncer's config (see `appsettings.example.json`). Bouncer is
the client here — it POSTs to you. This is the contract your server must implement.

## Request

```
POST <your configured Url>
Content-Type: application/json
Authorization: Bearer <token>        (only if a bearer token is configured for the route)
```

- Method is always `POST`. There is no signature/HMAC scheme — auth is a static bearer token
  per route (or none, if you didn't configure one).
- One request represents one batch of bounce records for one route (one sending `From`
  address, or the catch-all default route). Bouncer never mixes routes in a single request.
- Batch size is capped at `Webhooks.MaxBatchSize` (default 200) failures per request. A sender
  with more pending failures than that gets multiple sequential requests, one per poll cycle.
- Request timeout is `Webhooks.TimeoutSeconds` (default 15s), enforced client-side by Bouncer.
  Your endpoint should respond well within that.

## Body

```json
{
  "route": "app1@example.com",
  "failures": [
    {
      "originalFrom": "app1@example.com",
      "email": "bob@example.com",
      "originalRecipient": "bob@example.com",
      "action": "failed",
      "statusCode": "5.1.1",
      "diagnosticCode": "smtp; 550 5.1.1 user unknown",
      "remoteMta": "mx.example.com",
      "bouncedAtUtc": "2026-09-13T10:15:00.0000000Z"
    }
  ]
}
```

Property names are `camelCase`. Field-by-field:

| Field | Type | Meaning |
|---|---|---|
| `route` | string | The route key this batch was dispatched for: the lowercased sender `From` address for a matched route, or `"__default__"` when delivered via the catch-all `DefaultRoute`. Matches every `failures[].originalFrom` in a matched-route batch, but not necessarily in a default-route batch (see below). |
| `failures[].originalFrom` | string, nullable | The `From` address of the original outbound message that bounced, lowercased. `null` if the DSN didn't carry enough information to recover it (this is also why the record landed on the default route instead of a matched one). |
| `failures[].email` | string | The address that actually bounced (RFC 3464 `Final-Recipient`, address-type prefix stripped). This is the recipient to act on — e.g. suppress. |
| `failures[].originalRecipient` | string, nullable | RFC 3464 `Original-Recipient`, if the remote MTA sent one (e.g. differs from `email` after address rewriting/aliasing). Often absent. |
| `failures[].action` | string, nullable | Raw RFC 3464 `Action` field. Usually `failed`, but can also be `delayed`, `delivered`, `relayed`, or `expanded` — **Bouncer does not filter by action**, so treat this as informational and branch on it if you only want hard failures. |
| `failures[].statusCode` | string, nullable | Raw RFC 3464 `Status` field, an RFC 3463 enhanced status code (e.g. `5.1.1` = permanent, no such user; `4.x.x` = transient). Use the leading digit to distinguish permanent vs. transient failures. |
| `failures[].diagnosticCode` | string, nullable | Raw RFC 3464 `Diagnostic-Code` (e.g. `smtp; 550 5.1.1 user unknown`) — free text from the remote MTA, for logging/debugging only. |
| `failures[].remoteMta` | string, nullable | The remote MTA that reported the failure (RFC 3464 `Remote-MTA`, address-type prefix stripped). |
| `failures[].bouncedAtUtc` | string | ISO 8601 UTC timestamp (round-trippable, e.g. `2026-09-13T10:15:00.0000000Z`) of when Bouncer recorded the bounce — not the original `Arrival-Date` from the DSN. |

Notes:

- `failures` is never empty — a batch of zero pending records is not dispatched.
- On the default route, `originalFrom` varies per item (any sender that didn't match a
  specific route lands here), so don't assume it equals `route`.
- There is no per-item ID or dedupe key in the payload. If your endpoint needs one, use
  `originalFrom` + `email` + `bouncedAtUtc` from the DSN side; Bouncer's own delivery guarantee
  is at the batch level (see below).

## DTOs

Copy/paste these to deserialize the payload.

**C#**

```csharp
public sealed record BouncePayload(string Route, IReadOnlyList<BouncePayloadItem> Failures);

public sealed record BouncePayloadItem(
    string? OriginalFrom,
    string Email,
    string? OriginalRecipient,
    string? Action,
    string? StatusCode,
    string? DiagnosticCode,
    string? RemoteMta,
    string BouncedAtUtc);
```

System.Text.Json maps these properties to/from the payload's `camelCase` JSON out of the box
(property name matching is case-insensitive by default) — no `[JsonPropertyName]` attributes
needed.

**TypeScript**

```typescript
interface BouncePayload {
  route: string;
  failures: BouncePayloadItem[];
}

interface BouncePayloadItem {
  originalFrom: string | null;
  email: string;
  originalRecipient: string | null;
  action: string | null;
  statusCode: string | null;
  diagnosticCode: string | null;
  remoteMta: string | null;
  bouncedAtUtc: string;
}
```

## Expected response

- Any **2xx** status code is treated as success. The response body is ignored entirely — return
  whatever you like (or nothing).
- Any non-2xx status, a connection error, or exceeding `Webhooks.TimeoutSeconds` is treated as
  failure for the whole batch.

## Retry behavior — at-least-once delivery

Bouncer treats each POST as all-or-nothing per batch:

- On success, every record in that batch is marked `reported` and never sent again.
- On failure, **the entire batch is retried** — including any records your endpoint may have
  already durably processed if it failed partway through (e.g. accepted the request but crashed
  before responding). Design your endpoint to be **idempotent**: safe to receive the same
  `email` (per `originalFrom`) more than once.
- Retries are scheduled per route with exponential backoff (`Retry.InitialBackoffSeconds`,
  `Retry.BackoffMultiplier`, capped at `Retry.MaxBackoffSeconds`; defaults 60s → 3600s max). A
  failing route backs off independently — it never blocks other routes' dispatch.
- There is no maximum retry count / dead-lettering. A route that's down indefinitely will
  accumulate pending records forever (bounded only by `Webhooks.MaxBatchSize` per request) until
  it starts responding 2xx again.

## Minimal reference handler

```
POST /hooks/bounces
Authorization: Bearer <your-configured-token>
Content-Type: application/json

{ "route": "...", "failures": [ { ... }, ... ] }
```

**C# (ASP.NET Core, minimal API)**

```csharp
app.MapPost("/hooks/bounces", async (HttpContext ctx, BouncePayload payload) =>
{
    var auth = ctx.Request.Headers.Authorization.ToString();
    if (auth != $"Bearer {expectedToken}")
        return Results.Unauthorized();

    foreach (var failure in payload.Failures)
    {
        // Upsert-style write keyed on failure.Email — safe to see the same
        // email again if Bouncer retries this batch.
        await SuppressRecipientAsync(failure.Email, failure.StatusCode);
    }

    return Results.Ok(); // any 2xx acknowledges the whole batch
});
```

**TypeScript (Node + Express)**

```typescript
import express from "express";

const app = express();
app.use(express.json());

app.post("/hooks/bounces", async (req, res) => {
  if (req.headers.authorization !== `Bearer ${expectedToken}`) {
    return res.sendStatus(401);
  }

  const payload = req.body as BouncePayload;
  for (const failure of payload.failures) {
    // Upsert-style write keyed on failure.email — safe to see the same
    // email again if Bouncer retries this batch.
    await suppressRecipient(failure.email, failure.statusCode);
  }

  res.sendStatus(200); // any 2xx acknowledges the whole batch
});
```
