import { spawn } from "node:child_process";
export function run(
  exe,
  args,
  { input, env, timeout = 180000, signal, limit = 8 * 1024 * 1024 } = {},
) {
  return new Promise((resolve, reject) => {
    const child = spawn(exe, args, {
      windowsHide: true,
      env: env || process.env,
      stdio: ["pipe", "pipe", "pipe"],
    });
    let out = "",
      err = "",
      ended = false;
    const finish = (e, result) => {
      if (ended) return;
      ended = true;
      clearTimeout(timer);
      signal?.removeEventListener("abort", cancel);
      e ? reject(e) : resolve(result);
    };
    const cancel = () => {
      child.kill();
      finish(new Error("操作已取消或超时。"));
    };
    const timer = setTimeout(cancel, timeout);
    signal?.addEventListener("abort", cancel, { once: true });
    child.on("error", () => finish(new Error("无法启动本地组件。")));
    child.stdout.on("data", (d) => {
      out += d;
      if (out.length > limit) cancel();
    });
    child.stderr.on("data", (d) => {
      err = (err + d).slice(-65536);
    });
    child.on("close", (code) => finish(null, { code, out, err }));
    child.stdin.on("error", () => {});
    child.stdin.end(input ?? "");
    if (signal?.aborted) cancel();
  });
}
