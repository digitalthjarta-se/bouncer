const receiptsElement = document.querySelector("#receipts");
const emptyStateElement = document.querySelector("#empty-state");
const summaryElement = document.querySelector("#summary");
const clearButton = document.querySelector("#clear-button");
const receiptTemplate = document.querySelector("#receipt-template");

let renderedSignature = "";

function appendCell(row, value) {
  const cell = document.createElement("td");
  cell.textContent = value || "—";
  row.append(cell);
}

function render(receipts) {
  const signature = JSON.stringify(receipts);
  if (signature === renderedSignature)
    return;

  renderedSignature = signature;
  summaryElement.classList.remove("error");
  receiptsElement.replaceChildren();

  const failureCount = receipts.reduce(
    (total, receipt) => total + receipt.payload.failures.length,
    0);
  const hasReceipts = receipts.length > 0;

  emptyStateElement.hidden = hasReceipts;
  clearButton.disabled = !hasReceipts;
  summaryElement.textContent = hasReceipts
    ? `${failureCount} bounced address${failureCount === 1 ? "" : "es"} across ${receipts.length} batch${receipts.length === 1 ? "" : "es"}.`
    : "Waiting for a webhook batch.";

  for (const receipt of receipts) {
    const fragment = receiptTemplate.content.cloneNode(true);
    const article = fragment.querySelector(".receipt");
    const heading = fragment.querySelector("h3");
    const receivedAt = fragment.querySelector(".received-at");
    const count = fragment.querySelector(".count");
    const body = fragment.querySelector("tbody");

    article.dataset.receiptId = receipt.id;
    heading.textContent = receipt.payload.route;
    receivedAt.textContent = new Date(receipt.receivedAtUtc).toLocaleString();
    count.textContent = `${receipt.payload.failures.length} ${receipt.payload.failures.length === 1 ? "address" : "addresses"}`;

    for (const failure of receipt.payload.failures) {
      const row = document.createElement("tr");
      appendCell(row, failure.email);
      appendCell(row, failure.originalFrom);
      appendCell(row, failure.statusCode);
      appendCell(row, failure.action);
      appendCell(row, failure.remoteMta);
      body.append(row);
    }

    receiptsElement.append(fragment);
  }
}

async function refresh() {
  try {
    const response = await fetch("/api/receipts", { cache: "no-store" });
    if (!response.ok)
      throw new Error(`HTTP ${response.status}`);

    render(await response.json());
  } catch (error) {
    summaryElement.textContent = `Could not refresh the log: ${error.message}`;
    summaryElement.classList.add("error");
  }
}

clearButton.addEventListener("click", async () => {
  clearButton.disabled = true;
  const response = await fetch("/api/receipts", { method: "DELETE" });

  if (!response.ok) {
    summaryElement.textContent = `Could not clear the log: HTTP ${response.status}`;
    summaryElement.classList.add("error");
    clearButton.disabled = false;
    return;
  }

  renderedSignature = "";
  await refresh();
});

refresh();
setInterval(refresh, 1000);
