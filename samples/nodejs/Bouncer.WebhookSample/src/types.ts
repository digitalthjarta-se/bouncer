export interface BouncePayload {
  route: string;
  failures: BouncePayloadItem[];
}

export interface BouncePayloadItem {
  originalFrom: string | null;
  email: string;
  originalRecipient: string | null;
  action: string | null;
  statusCode: string | null;
  diagnosticCode: string | null;
  remoteMta: string | null;
  bouncedAtUtc: string;
}

export interface WebhookReceipt {
  id: number;
  receivedAtUtc: string;
  payload: BouncePayload;
}

export function isBouncePayload(value: unknown): value is BouncePayload {
  if (!isRecord(value) || typeof value.route !== "string" || !Array.isArray(value.failures))
    return false;

  return value.failures.every(
    failure => isRecord(failure) && typeof failure.email === "string");
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}
