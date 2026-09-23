import type { BouncePayload, WebhookReceipt } from "./types.js";

const maxReceipts = 100;

export class ReceiptLog {
  private readonly receipts: WebhookReceipt[] = [];
  private nextId = 0;

  add(payload: BouncePayload): WebhookReceipt {
    const receipt: WebhookReceipt = {
      id: ++this.nextId,
      receivedAtUtc: new Date().toISOString(),
      payload,
    };

    this.receipts.push(receipt);
    if (this.receipts.length > maxReceipts)
      this.receipts.shift();

    return receipt;
  }

  getAll(): readonly WebhookReceipt[] {
    return [...this.receipts].reverse();
  }

  clear(): void {
    this.receipts.length = 0;
  }
}
