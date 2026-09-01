import { existsSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const packageJsonPath = join(root, "package.json");
const packageJson = JSON.parse(readFileSync(packageJsonPath, "utf8"));
const dependencies = {
  ...(packageJson.dependencies ?? {}),
  ...(packageJson.devDependencies ?? {})
};

const required = [
  "react",
  "react-dom",
  "typescript",
  "vite",
  "@vitejs/plugin-react",
  "ai",
  "@ai-sdk/react",
  "@assistant-ui/react",
  "@assistant-ui/react-ai-sdk",
  "pdfjs-dist"
];

const missing = required.filter((name) => {
  if (!dependencies[name]) return true;
  return !existsSync(join(root, "node_modules", name, "package.json"));
});

if (missing.length) {
  console.error(JSON.stringify({
    ok: false,
    error: "AssistantWeb dependencies are not installed. Do not edit generated dist from stale source; install dependencies first, then run npm run build.",
    missing,
    installCommand: "npm install"
  }, null, 2));
  process.exit(1);
}

console.log(JSON.stringify({
  ok: true,
  checked: required
}, null, 2));
