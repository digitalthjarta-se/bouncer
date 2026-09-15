# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

Bouncer is used by operators and application developers who maintain outbound email systems and need bounced-recipient data routed back to the application that sent the message.

## Product Purpose

Bouncer monitors a shared POP3 bounce mailbox, parses delivery-status notifications, and dispatches failed recipient addresses to per-sender webhooks. Success means bounce mail is handled reliably, each application receives the failures that belong to it, and the shared mailbox returns to inbox zero.

## Positioning

Bouncer combines mailbox ingestion, RFC 3464 parsing, sender-based webhook routing, durable local state, and independent delivery retries in one small self-hosted worker.

## Operating Context

The worker runs unattended as a .NET service or container. Operators configure POP3 access, webhook routes, retry behavior, and a local SQLite database. Developers use the webhook contract and test fixtures while integrating or debugging recipient suppression.

## Capabilities and Constraints

- Receives mail through POP3 and parses RFC 3464 delivery-status notifications.
- Routes batches by the original sender address, with an optional catch-all route.
- Delivers JSON webhooks with at-least-once semantics and per-route exponential backoff.
- Persists processing state in SQLite.
- Uses optional static bearer tokens for webhook authentication.
- The sample webhook is a local development aid, not a durable production event store.

## Evidence on Hand

The repository contains the worker, webhook DTOs and sender, configuration examples, webhook contract documentation, automated tests, and representative `.eml` fixtures. No customer claims, benchmarks, or production usage evidence are present.

## Product Principles

- Make delivery behavior observable and straightforward to debug.
- Preserve bounce data faithfully rather than inferring recipient policy.
- Fail clearly and retry safely.
- Keep deployment and local development operationally small.

