import { CODEGEN_CONFIG, CODEGEN_PLUGINS } from "@vc-frontend/core/codegen";
import { loadEnv } from "vite";
import type { CodegenConfig } from "@graphql-codegen/cli";

// graphql-codegen reads no .env of its own; reuse Vite's loader so one file serves both. The
// shell wins over .env.
const env = { ...loadEnv("", process.cwd(), ""), ...process.env };

if (!env.APP_BACKEND_URL) {
  throw new Error("APP_BACKEND_URL is not set - copy .env.example to .env, or export it.");
}

const codegen: CodegenConfig = {
  // The schema the module registers under this name: appBuilder.UseScopedSchema<…>("product-subscription")
  // in its Module.cs. A module that extends the storefront schema instead: plain `/graphql`.
  schema: `${env.APP_BACKEND_URL}/graphql/product-subscription`,
  documents: "src/api/graphql/**/*.graphql",
  // Scalars and plugins come from the host so the same backend value never gets two different
  // TypeScript types.
  generates: { "src/api/graphql/types.ts": { plugins: CODEGEN_PLUGINS, config: CODEGEN_CONFIG } },
};

export default codegen;
