import { stripTypeScriptTypes } from "node:module";
import { readdir, readFile, writeFile, mkdir } from "node:fs/promises";
import { fileURLToPath } from "node:url";
const base = fileURLToPath(new URL("./siwc/", import.meta.url));
await mkdir(base + "dist", { recursive: true });
for (const f of await readdir(base + "src")) {
  if (!f.endsWith(".ts")) continue;
  const result = stripTypeScriptTypes(
    await readFile(base + "src/" + f, "utf8"),
    { mode: "transform" },
  );
  await writeFile(base + "dist/" + f.replace(/\.ts$/, ".js"), result);
}
console.log("Built pinned SIWC DevKit with inline-image support.");
