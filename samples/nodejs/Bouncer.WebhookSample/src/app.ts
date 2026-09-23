import express, { type ErrorRequestHandler } from "express";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { ReceiptLog } from "./receipt-log.js";
import { isBouncePayload } from "./types.js";

const publicDirectory = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  "../public");

export function createApp(
  receiptLog = new ReceiptLog(),
  expectedToken = process.env.WEBHOOK_BEARER_TOKEN,
) {
  const app = express();

  app.use(express.json({ limit: "1mb" }));
  app.use(express.static(publicDirectory));

  app.post("/hooks/bounces", (request, response) => {
    if (expectedToken &&
        request.header("authorization") !== `Bearer ${expectedToken}`) {
      console.warn("Rejected webhook request with invalid bearer token");
      response.sendStatus(401);
      return;
    }

    if (!isBouncePayload(request.body)) {
      response.status(400).json({
        error: "Expected a bounce payload with a route and failures array.",
      });
      return;
    }

    const receipt = receiptLog.add(request.body);
    console.info(
      `Received webhook batch ${receipt.id} for route ${request.body.route} ` +
      `with ${request.body.failures.length} failure(s)`);

    for (const failure of request.body.failures) {
      console.info(
        `Bounced address: ${failure.email} ` +
        `(from: ${failure.originalFrom ?? "unknown"}, ` +
        `status: ${failure.statusCode ?? "unknown"})`);
    }

    response.json({ id: receipt.id });
  });

  app.get("/api/receipts", (_request, response) => {
    response.json(receiptLog.getAll());
  });

  app.delete("/api/receipts", (_request, response) => {
    receiptLog.clear();
    response.sendStatus(204);
  });

  const jsonErrorHandler: ErrorRequestHandler = (error, _request, response, next) => {
    if (error instanceof SyntaxError) {
      response.status(400).json({ error: "Request body must be valid JSON." });
      return;
    }

    next(error);
  };
  app.use(jsonErrorHandler);

  return app;
}
