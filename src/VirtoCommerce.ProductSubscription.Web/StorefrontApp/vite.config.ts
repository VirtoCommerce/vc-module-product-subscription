import { federation } from "@module-federation/vite";
import { createRemoteFederationOptions } from "@vc-frontend/core/federation";
import { pluginContributions } from "@vc-frontend/core/manifest";
import vue from "@vitejs/plugin-vue";
import { defineConfig } from "vite";
import contributions from "./plugin.config";

export default defineConfig({
  plugins: [
    vue(),
    // Writes plugin.config.ts into the built plugin.json.
    pluginContributions(contributions),
    // Wiring conventions (expose key, shared singletons, manifest metadata) come from
    // the host - client-app/core-api/federation.mjs in the host checkout owns them.
    federation(
      createRemoteFederationOptions({
        name: "product-subscription",
        // CONTRACT GATE: the facade versions this plugin runs on. A host OUTSIDE the range is
        // refused before any of your code runs. Keep it ^ the facade you pin: check:plugin-module
        // fails a range that reaches below the pin or past its breaking line (README, rule 5).
        requiredHostVersion: "^0.2.17-alpha.12.vcst-6507",
      }),
    ),
  ],
  build: {
    target: "esnext", // MF entry uses top-level await
    // The platform probes {moduleRoot}/plugins/vc-frontend/ of every installed module.
    outDir: "../plugins/vc-frontend",
    emptyOutDir: true,
  },
  // No cors option: Vite's default lets in localhost origins only, which is where a local storefront runs.
  server: { port: 3001, origin: "http://localhost:3001" },
});
