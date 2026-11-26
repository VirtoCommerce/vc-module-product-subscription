import { fileURLToPath } from "node:url";
import vue from "@vitejs/plugin-vue";
import { defineConfig } from "vitest/config";

export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: [
      // The facade is types-only at runtime, so Vite has no entry to resolve and a spec dies
      // before vi.mock() can substitute anything. A regex, not a string: a string key matches by
      // PREFIX and would swallow "@vc-frontend/core/testing" too.
      {
        find: /^@vc-frontend\/core$/,
        replacement: fileURLToPath(new URL("./src/mocks/vc-frontend-core.ts", import.meta.url)),
      },
    ],
  },
  test: { environment: "jsdom", passWithNoTests: true },
});
