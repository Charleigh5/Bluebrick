import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

function escapeHtmlAttribute(value) {
  return value.replaceAll("&", "&amp;").replaceAll('"', "&quot;").replaceAll("<", "&lt;").replaceAll(">", "&gt;");
}

export function runtimeBuildIdentityPlugin(buildId = process.env.VITE_BLUEBRICK_BUILD_ID) {
  return {
    name: "bluebrick-runtime-build-identity",
    transformIndexHtml(html) {
      const normalized = typeof buildId === "string" ? buildId.trim() : "";
      if (!normalized || normalized.toUpperCase() === "UNKNOWN") throw new Error("VITE_BLUEBRICK_BUILD_ID must identify the built frontend.");
      const tokens = /<!--[\s\S]*?(?:-->|$)|<(script|style|textarea|title|xmp|iframe|noembed|noframes|template)\b[^>]*>[\s\S]*?(?:<\/\1\s*>|$)|<plaintext\b[\s\S]*$|<[a-z][a-z0-9:-]*(?=\s|\/?>)(?:[^>"']|"[^"]*"|'[^']*')*>/gi;
      let inserted = false;
      const transformed = html.replace(tokens, (tag) => {
        if (/^<meta(?=\s|\/?>)/i.test(tag)) {
          const attributes = [...tag.slice(5, -1).matchAll(/([^\s=/>]+)(?:\s*=\s*(?:"([^"]*)"|'([^']*)'|([^\s>]+)))?/g)];
          if (attributes.some((attribute) => attribute[1].toLowerCase() === "name" && (attribute[2] ?? attribute[3] ?? attribute[4] ?? "").toLowerCase() === "bluebrick-build-id")) throw new Error("Duplicate bluebrick-build-id metadata.");
        }
        if (!inserted && /^<head(?=\s|>)/i.test(tag)) {
          inserted = true;
          return `${tag}\n    <meta name="bluebrick-build-id" content="${escapeHtmlAttribute(normalized)}">`;
        }
        return tag;
      });
      if (!inserted) throw new Error("Missing HTML head for build identity.");
      return transformed;
    }
  };
}

export default defineConfig({
  base: "./",
  define: { "import.meta.env.VITE_BLUEBRICK_BUILD_ID": JSON.stringify(process.env.VITE_BLUEBRICK_BUILD_ID?.trim() ?? "") },
  plugins: [react(), runtimeBuildIdentityPlugin()],
  build: {
    outDir: "dist",
    emptyOutDir: true,
    rollupOptions: {
      output: {
        entryFileNames: "assistant-web.js",
        chunkFileNames: "assistant-[name].js",
        assetFileNames: "assistant-[name][extname]"
      }
    }
  }
});
