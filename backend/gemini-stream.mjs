// Gemini's documented SSE generate-content endpoint. Keep transport parsing
// separate so fragmented UTF-8, cancellation and incomplete replies are testable.
export async function readGeminiStream(response, onDelta) {
  if (!response.ok) {
    await response.body?.cancel();
    throw new Error(
      "Gemini 请求失败（HTTP " +
        response.status +
        "）。请检查 Key、权限和额度。",
    );
  }
  const decoder = new TextDecoder();
  let buffer = "",
    text = "",
    finished = false;
  const event = (value) => {
    const data = value
      .split(/\r?\n/)
      .filter((x) => x.startsWith("data:"))
      .map((x) => x.slice(5).trimStart())
      .join("\n");
    if (!data) return;
    const message = JSON.parse(data);
    if (message.error || message.promptFeedback?.blockReason)
      throw new Error("Gemini 未能完成回答。请调整问题后重试。");
    const candidate = message.candidates?.find((x) => (x.index ?? 0) === 0);
    if (!candidate) return;
    const delta = (candidate.content?.parts || [])
      .filter((x) => !x.thought)
      .map((x) => x.text || "")
      .join("");
    if (delta) {
      text += delta;
      if (text.length > 2 * 1024 * 1024)
        throw new Error("回答过长，请缩小问题范围。");
      onDelta(delta);
    }
    if (candidate.finishReason) {
      if (candidate.finishReason !== "STOP")
        throw new Error(
          "Gemini 回答未完成（" + candidate.finishReason + "）。",
        );
      finished = true;
    }
  };
  for await (const chunk of response.body) {
    buffer += decoder.decode(chunk, { stream: true });
    let match;
    while ((match = /\r?\n\r?\n/.exec(buffer))) {
      event(buffer.slice(0, match.index));
      buffer = buffer.slice(match.index + match[0].length);
    }
    if (buffer.length > 2 * 1024 * 1024)
      throw new Error("Gemini 响应格式异常。");
  }
  buffer += decoder.decode();
  if (buffer.trim()) event(buffer);
  if (!finished) throw new Error("回答中断，请重试。");
  return text;
}

export async function streamGemini(key, model, contents, signal, onDelta) {
  const response = await fetch(
    "https://generativelanguage.googleapis.com/v1beta/models/" +
      encodeURIComponent(model) +
      ":streamGenerateContent?alt=sse",
    {
      method: "POST",
      redirect: "error",
      signal,
      headers: { "x-goog-api-key": key, "content-type": "application/json" },
      body: JSON.stringify({ contents }),
    },
  );
  return readGeminiStream(response, onDelta);
}
