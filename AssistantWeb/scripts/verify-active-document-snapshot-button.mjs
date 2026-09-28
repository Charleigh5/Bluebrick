import { existsSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const indexPath = join(root, "dist", "index.html");

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

assert(existsSync(indexPath), "React dist index is missing; build before snapshot-button verification.");

const { chromium } = await import("playwright");
const browser = await chromium.launch({
  headless: true,
  args: ["--allow-file-access-from-files"],
});

try {
  const page = await browser.newPage({ viewport: { width: 480, height: 720 } });
  const networkRequests = [];
  page.on("request", (request) => {
    if (!request.url().startsWith("file:")) networkRequests.push(request.url());
  });
  await page.addInitScript(() => {
    window.__bbMessages = [];
    window.__blueBrickDocumentNonce = "ui-test-nonce";
    window.chrome = {
      webview: {
        postMessage: (message) => {
          window.__bbMessages.push(message);
          if (message?.type !== "captureActiveDocumentSnapshot") return;
          setTimeout(() => {
            window.bbAppendToolResult({
              label: "Active Document Snapshot",
              query: "",
              status: "ok",
              message: "Snapshot captured.",
              items: [{
                Id: "snapshot",
                Title: "Part",
                Metadata: {
                  mutation_count: "0",
                  runtime: "33.5.0",
                  duration_ms: "0",
                  correlation: "ui-test-correlation",
                  mode: "READ_ONLY_ANALYST",
                  status: "ok",
                },
              }],
              receipt: {
                receiptId: "ui-test-receipt",
                traceId: "ui-test-correlation",
                toolName: "solidworks.get_active_document_snapshot",
                riskLevel: "low",
                allowed: true,
                approvalRequired: false,
                policyCode: "safe_tool_name",
                resultStatus: "ok",
              },
            });
          }, 0);
        },
      },
    };
  });
  await page.goto(pathToFileURL(indexPath).href);
  await page.waitForSelector(".shell", { timeout: 10000 });
  await page.getByRole("button", { name: "Customize" }).click();
  await page.locator(".toolbar-catalog button").filter({ hasText: "Snapshot" }).click();
  const trigger = page.locator('button[aria-label="Snapshot"]');
  await trigger.waitFor({ state: "visible", timeout: 10000 });
  const before = await page.evaluate(() => (window.__bbMessages || []).length);
  await trigger.click();
  await page.waitForSelector('.active-document-card[data-context-state="ready"]', { timeout: 10000 });
  const posted = await page.evaluate((count) => (window.__bbMessages || []).slice(count), before);
  const cardText = await page.locator('.active-document-card[data-context-state="ready"]').innerText();
  const allMessages = await page.evaluate(() => window.__bbMessages || []);

  assert(posted.length === 1, `Snapshot click must post exactly one message: ${JSON.stringify(posted)}`);
  assert(posted[0].type === "captureActiveDocumentSnapshot", `Unexpected host message: ${JSON.stringify(posted[0])}`);
  assert(posted[0].documentNonce === "ui-test-nonce", "Snapshot host message must carry the document nonce");
  assert(allMessages.every((message) => message.type !== "sendMessage"), "Snapshot action must not post a model message");
  assert(cardText.includes("Part"), `Snapshot card is missing the document type: ${cardText}`);
  assert(cardText.includes("33.5.0"), `Snapshot card is missing the runtime version: ${cardText}`);
  assert(cardText.includes("0 mutation actions"), `Snapshot card is missing the read-only boundary: ${cardText}`);
  assert(networkRequests.length === 0, `Snapshot UI must not make network requests: ${networkRequests.join(", ")}`);

  const offline = await browser.newPage({ viewport: { width: 480, height: 720 } });
  await offline.goto(pathToFileURL(indexPath).href);
  await offline.waitForSelector(".shell", { timeout: 10000 });
  await offline.getByRole("button", { name: "Customize" }).click();
  await offline.locator(".toolbar-catalog button").filter({ hasText: "Snapshot" }).click();
  await offline.locator('button[aria-label="Snapshot"]').click();
  await offline.waitForSelector('[role="alert"]', { timeout: 10000 });
  const offlineText = await offline.locator('[role="alert"]').innerText();
  assert(offlineText.includes("host is offline"), `Offline error is not explicit: ${offlineText}`);

  console.log(JSON.stringify({
    ok: true,
    checked: [
      "toolbar_snapshot_action_posts_one_allowlisted_message",
      "document_nonce_is_attached",
      "host_result_renders_part_runtime_and_zero_mutations",
      "no_model_message_or_network_request",
      "offline_action_fails_visibly",
    ],
    posted,
    cardText,
    offlineText,
    source: indexPath,
    safetyBoundary: {
      usesFileSchemeOnly: true,
      startsListener: false,
      launchesSolidWorks: false,
      callsProvider: false,
      callsNetwork: false,
    },
  }, null, 2));
} finally {
  await browser.close();
}
