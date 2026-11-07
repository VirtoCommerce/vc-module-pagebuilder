import path from "node:path";
import { getDynamicModuleConfiguration } from "@vc-shell/mf-module";

// Page Builder contributions to the Admin Shell: a Module Federation remote of the Admin Shell host.
// Output: <module>/plugins/admin-shell/ (remoteEntry.js + public/plugin.json),
// served by the platform at /modules/$(VirtoCommerce.PageBuilderModule)/plugins/admin-shell/.
const base = "/modules/$(VirtoCommerce.PageBuilderModule)/plugins/admin-shell/";
process.env.APP_BASE_PATH = base;

export default getDynamicModuleConfiguration({
  appId: "admin-shell",
  moduleRoot: path.resolve(__dirname, ".."),
  entry: "./src/modules/index.ts",
  remoteName: "pagebuilder_admin",
  base,
});
