import test from "node:test";
import assert from "node:assert/strict";
import { readGeminiStream } from "../gemini-stream.mjs";
const event = (text, finishReason) =>
  "data: " +
  JSON.stringify({
    candidates: [{ content: { parts: [{ text }] }, finishReason }],
  }) +
  "\r\n\r\n";
test("fragmented UTF-8 SSE emits incremental deltas and preserves Chinese", async () => {
  const bytes = new TextEncoder().encode(event("你好") + event("世界", "STOP"));
  let index = 0;
  const stream = new ReadableStream({
    pull(c) {
      if (index === bytes.length) c.close();
      else c.enqueue(bytes.slice(index, (index += 1)));
    },
  });
  const deltas = [];
  assert.equal(
    await readGeminiStream(new Response(stream), (x) => deltas.push(x)),
    "你好世界",
  );
  assert.deepEqual(deltas, ["你好", "世界"]);
});
test("incomplete or blocked streams fail instead of committing success", async () => {
  await assert.rejects(
    () => readGeminiStream(new Response(event("partial")), () => {}),
    /中断/,
  );
  await assert.rejects(
    () =>
      readGeminiStream(new Response(event("partial", "MAX_TOKENS")), () => {}),
    /未完成/,
  );
  await assert.rejects(
    () =>
      readGeminiStream(
        new Response('data: {"promptFeedback":{"blockReason":"SAFETY"}}\n\n'),
        () => {},
      ),
    /未能完成/,
  );
});
test("transport cancellation propagates and thought parts are excluded", async () => {
  const abort = new ReadableStream({
    start(c) {
      c.error(new DOMException("Cancelled", "AbortError"));
    },
  });
  await assert.rejects(() => readGeminiStream(new Response(abort), () => {}), {
    name: "AbortError",
  });
  const body =
    "data: " +
    JSON.stringify({
      candidates: [
        {
          content: {
            parts: [
              { text: "private reasoning", thought: true },
              { text: "visible" },
            ],
          },
          finishReason: "STOP",
        },
      ],
    }) +
    "\n\n";
  assert.equal(await readGeminiStream(new Response(body), () => {}), "visible");
});
