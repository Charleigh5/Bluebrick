import { existsSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const indexPath = join(root, "dist", "index.html");

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

assert(existsSync(indexPath), "React dist index is missing; build before replay verification.");

const { chromium } = await import("playwright");
const browser = await chromium.launch({
  headless: true,
  args: ["--allow-file-access-from-files"],
});
try {
  const page = await browser.newPage({ viewport: { width: 480, height: 720 } });
  await page.addInitScript(() => {
    window.__blueBrickReplayMessages = [];
    window.chrome = {
      webview: {
        postMessage: (message) => {
          window.__blueBrickReplayMessages.push(message);
          if (!message || message.type !== "sendMessage") return;

          // File-only host stub: the rendered Send action crosses the real
          // browser->host transport seam, then the stub asynchronously echoes
          // the response through the same host->browser callbacks used by the
          // C# panel. No test step mutates the transcript directly.
          setTimeout(() => {
            if (message.message === "round-1 user") {
              window.bbAppend({ role: "assistant", text: "round-1 final" });
              return;
            }
            if (message.message === "round-2 user") {
              window.bbAppendChunk("round-2 error: local host unavailable");
              window.bbTypingStop();
            }
          }, 0);
        },
      },
    };
  });
  await page.goto(pathToFileURL(indexPath).href);
  await page.waitForSelector(".shell", { timeout: 10000 });
  await page.waitForSelector('textarea[aria-label="Message BlueBrick Assistant"]', { timeout: 10000 });
  await page.waitForFunction(() => typeof window.bbGetTranscript === "function");

  const sendRenderedMessage = async (text) => {
    const before = await page.evaluate(() => window.__blueBrickReplayMessages.length);
    await page.locator('textarea[aria-label="Message BlueBrick Assistant"]').fill(text);
    await page.locator('button[aria-label="Send message"]').click();
    await page.waitForFunction(
      (count) => (window.__blueBrickReplayMessages || [])
        .slice(count)
        .some((message) => message && message.type === "sendMessage"),
      before,
    );
    return page.evaluate((count) => (window.__blueBrickReplayMessages || [])
      .slice(count)
      .filter((message) => message && message.type === "sendMessage"), before);
  };

  const waitForTranscript = async (expected) => {
    await page.waitForFunction(
      (value) => JSON.stringify(window.bbGetTranscript()) === JSON.stringify(value),
      expected,
    );
  };

  const result = await page.evaluate(() => {
    const expectedCallbacks = [
      "bbReset", "bbAppend", "bbTypingStart", "bbAppendChunk", "bbTypingStop",
      "bbSetModel", "bbSetModels", "bbSetScope", "bbSetScopes", "bbSetStatus",
      "bbSetTools", "bbSetToolReceipts", "bbSetProductCatalogs",
      "bbAppendToolResult", "bbAppendScreenshotArtifact",
      "bbUpdateScreenshotArtifact", "bbGetTranscript",
    ];
    const missingCallbacks = expectedCallbacks.filter((name) => typeof window[name] !== "function");

    window.bbReset();
    return { missingCallbacks };
  });

  assert(result.missingCallbacks.length === 0, `Missing callbacks: ${result.missingCallbacks.join(", ")}`);

  const firstSend = await sendRenderedMessage("round-1 user");
  assert(firstSend.length === 1, `Rendered Send must produce one sendMessage host request: ${JSON.stringify(firstSend)}`);
  assert(firstSend[0].message === "round-1 user", `First host request message mismatch: ${JSON.stringify(firstSend[0])}`);

  const afterRoundOne = [
    { role: "user", text: "round-1 user" },
    { role: "assistant", text: "round-1 final" },
  ];
  await waitForTranscript(afterRoundOne);

  await page.locator('button[aria-label="Send message"]').waitFor({ state: "visible" });
  const secondSend = await sendRenderedMessage("round-2 user");
  assert(secondSend.length === 1, `Second rendered Send must produce one sendMessage host request: ${JSON.stringify(secondSend)}`);
  assert(secondSend[0].message === "round-2 user", `Second host request message mismatch: ${JSON.stringify(secondSend[0])}`);

  const expectedReplay = [
    ...afterRoundOne,
    { role: "user", text: "round-2 user" },
    { role: "assistant", text: "round-2 error: local host unavailable" },
  ];
  await waitForTranscript(expectedReplay);
  const replay = await page.evaluate(() => window.bbGetTranscript());
  assert(replay.length === 4, `Two chat rounds must leave exactly four records: ${JSON.stringify(replay)}`);
  assert(JSON.stringify(replay) === JSON.stringify(expectedReplay), `Two chat rounds must replay four ordered, non-duplicated records: ${JSON.stringify(replay)}`);

  console.log(JSON.stringify({
    ok: true,
    checked: [
      "exact_17_callbacks_available",
      "rendered_send_posts_first_host_request",
      "host_echo_finalizes_first_pending_assistant_record",
      "rendered_send_posts_second_host_request",
      "streamed_error_finalizes_second_pending_assistant_record",
      "two_round_four_record_ordered_replay",
    ],
    sentMessages: [firstSend[0], secondSend[0]],
    afterRoundOne,
    replay,
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
