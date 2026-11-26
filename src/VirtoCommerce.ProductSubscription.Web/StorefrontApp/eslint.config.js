import { defineConfigWithVueTs, vueTsConfigs } from "@vue/eslint-config-typescript";
import prettier from "eslint-plugin-prettier/recommended";
import pluginVue from "eslint-plugin-vue";
import globals from "globals";

// The host's flat config, trimmed to what a standalone plugin needs.
export default defineConfigWithVueTs(
  { ignores: ["dist/", "node_modules/", ".yalc/", "src/api/graphql/types.ts"] },
  pluginVue.configs["flat/recommended"],
  vueTsConfigs.recommended,
  { languageOptions: { globals: { ...globals.browser } } },
  {
    rules: {
      // `_`-prefixed is the deliberate "unused" convention.
      "@typescript-eslint/no-unused-vars": ["error", { argsIgnorePattern: "^_", varsIgnorePattern: "^_" }],
      // Both off in the host: pages are named after their route segment, props use `defineProps<IProps>()`.
      "vue/multi-word-component-names": "off",
      "vue/require-default-prop": "off",
    },
  },
  {
    files: ["**/*.cjs"],
    rules: { "@typescript-eslint/no-require-imports": "off" },
  },
  prettier,
);
