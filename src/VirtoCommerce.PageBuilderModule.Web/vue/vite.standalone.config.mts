import path from "node:path";

// The config generator reads APP_* when it loads, hence the dynamic import after setting them.
process.env.APP_BASE_PATH = "/apps/page-builder-shell/";
process.env.APP_I18N_LOCALE ||= "en";
process.env.APP_I18N_FALLBACK_LOCALE ||= "en";

const { getApplicationConfiguration } = await import("@vc-shell/config-generator");

const config = getApplicationConfiguration({
  root: path.resolve(__dirname, "standalone"),
  build: { outDir: path.resolve(__dirname, "../Apps/page-builder-shell/dist") },
});

// `yarn type-check` covers both builds, so this build drops the checker plugin.
config.plugins = config.plugins?.filter((p) => !(p && "name" in p && p.name === "vite-plugin-checker"));

export default config;
