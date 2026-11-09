import path from "node:path";
import vue from "@vitejs/plugin-vue";
import { defineConfig } from "vitest/config";

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
