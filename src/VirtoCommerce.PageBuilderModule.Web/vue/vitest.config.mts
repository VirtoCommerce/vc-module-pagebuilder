import path from "node:path";
import vue from "@vitejs/plugin-vue";
import { defineConfig } from "vitest/config";

// Component tests (tests/components/**/*.spec.ts, `yarn test`): the designer's components and composables in jsdom.
// vc-shell is replaced by tests/components/support (its UI kit by plain elements), so the tests are about the
// designer's own logic; the platform is a fake behind `fetch`. tests/*.test.ts are the node:test ones.
const support = (file: string) => path.resolve(__dirname, "tests/components/support", file);

export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: [
      { find: /^@vc-shell\/framework\/ui$/, replacement: support("vcShellUi.ts") },
      { find: /^@vc-shell\/framework$/, replacement: support("vcShell.ts") },
    ],
  },
  test: {
    environment: "jsdom",
    include: ["tests/components/**/*.spec.ts"],
    setupFiles: [support("setup.ts")],
    unstubGlobals: true,
  },
});
