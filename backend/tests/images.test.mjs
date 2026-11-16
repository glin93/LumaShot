import assert from "node:assert/strict";
import test from "node:test";
import { streamResponse } from "../siwc/dist/responses.js";
test("SIWC image extension accepts only inline images and preserves stream completion", async (t) => {
  let received;
  t.mock.method(globalThis, "fetch", async (url, options) => {
    received = JSON.parse(options.body);
    return new Response(
      "data: " +
        JSON.stringify({ type: "response.output_text.delta", delta: "ok" }) +
        "\n\ndata: " +
        JSON.stringify({
          type: "response.completed",
          response: { status: "completed" },
        }) +
        "\n\n",
      { headers: { "content-type": "text/event-stream" } },
    );
  });
  const input = [
    {
      role: "user",
      content: [
        { type: "input_text", text: "describe" },
        {
          type: "input_image",
          image_url: "data:image/png;base64,aGVsbG8=",
          detail: "auto",
        },
      ],
    },
  ];
  assert.equal(
    (
      await streamResponse(
        "synthetic-token",
        { model: "test", input },
        new AbortController().signal,
      )
    ).text,
    "ok",
  );
  assert.deepEqual(received.input, input);
  assert.equal(received.store, false);
  assert.equal(received.stream, true);
  await assert.rejects(
    () =>
      streamResponse(
        "synthetic-token",
        {
          model: "test",
          input: [
            {
              role: "user",
              content: [
                {
                  type: "input_image",
                  image_url: "https://remote.invalid/picture.png",
                  detail: "auto",
                },
              ],
            },
          ],
        },
        new AbortController().signal,
      ),
    (e) => e.code === "invalid_request",
  );
});
