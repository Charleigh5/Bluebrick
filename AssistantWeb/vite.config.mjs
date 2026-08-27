import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  base: "./",
  plugins: [react()],
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
