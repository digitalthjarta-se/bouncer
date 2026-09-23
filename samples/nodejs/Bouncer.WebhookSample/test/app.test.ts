import assert from "node:assert/strict";
import { afterEach, test } from "node:test";
import type { Server } from "node:http";
import { createApp } from "../src/app.js";
import type { BouncePayload, WebhookReceipt } from "../src/types.js";

const servers: Server[] = [];
const payload: BouncePayload = {
  route: "app1@example.com",
  failures: [{
    originalFrom: "app1@example.com",
    email: "bob@example.com",
    originalRecipient: "bob@example.com",
    action: "failed",
    statusCode: "5.1.1",
    diagnosticCode: "smtp; 550 5.1.1 user unknown",
    remoteMta: "mx.example.com",
    bouncedAtUtc: "2026-09-13T10:15:00.0000000Z",
  }],
};

afterEach(async () => {
  await Promise.all(servers.splice(0).map(
    server => new Promise<void>((resolve, reject) => {
      server.close(error => error ? reject(error) : resolve());
    })));
});

test("receives, lists, and clears webhook batches", async () => {
  const baseUrl = await startApp();

  const postResponse = await fetch(`${baseUrl}/hooks/bounces`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(payload),
  });
  assert.equal(postResponse.status, 200);
  assert.deepEqual(await postResponse.json(), { id: 1 });

  const receiptsResponse = await fetch(`${baseUrl}/api/receipts`);
  const receipts = await receiptsResponse.json() as WebhookReceipt[];
  assert.equal(receipts.length, 1);
  assert.equal(receipts[0]?.payload.failures[0]?.email, "bob@example.com");

  const clearResponse = await fetch(`${baseUrl}/api/receipts`, { method: "DELETE" });
  assert.equal(clearResponse.status, 204);
  assert.deepEqual(await (await fetch(`${baseUrl}/api/receipts`)).json(), []);
});

test("checks the optional bearer token", async () => {
  const baseUrl = await startApp("local-test");

  const unauthorized = await fetch(`${baseUrl}/hooks/bounces`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(payload),
  });
  assert.equal(unauthorized.status, 401);

  const authorized = await fetch(`${baseUrl}/hooks/bounces`, {
    method: "POST",
    headers: {
      authorization: "Bearer local-test",
      "content-type": "application/json",
    },
    body: JSON.stringify(payload),
  });
  assert.equal(authorized.status, 200);
});

async function startApp(token?: string): Promise<string> {
  const server = createApp(undefined, token).listen(0);
  servers.push(server);

  await new Promise<void>(resolve => server.once("listening", resolve));
  const address = server.address();
  assert(address && typeof address === "object");
  return `http://127.0.0.1:${address.port}`;
}
