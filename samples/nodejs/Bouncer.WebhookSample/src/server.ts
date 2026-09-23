import { createApp } from "./app.js";

const port = parsePort(process.env.PORT);
const server = createApp().listen(port, () => {
  console.info(`Bouncer webhook receiver listening at http://localhost:${port}`);
  console.info(`Webhook endpoint: http://localhost:${port}/hooks/bounces`);
});

function parsePort(value: string | undefined): number {
  if (value === undefined)
    return 5100;

  const port = Number(value);
  if (!Number.isInteger(port) || port < 1 || port > 65_535)
    throw new Error(`PORT must be an integer between 1 and 65535; received "${value}".`);

  return port;
}

function shutdown(): void {
  server.close(error => {
    if (error) {
      console.error("Failed to stop the webhook receiver", error);
      process.exitCode = 1;
    }
  });
}

process.on("SIGINT", shutdown);
process.on("SIGTERM", shutdown);
