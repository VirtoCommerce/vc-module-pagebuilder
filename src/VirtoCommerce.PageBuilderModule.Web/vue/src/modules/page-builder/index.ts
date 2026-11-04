import * as pages from "./pages";
import * as locales from "./locales";
import { defineAppModule } from "@vc-shell/framework";
import { definePermalinkRule } from "./permalinkRule";

definePermalinkRule();

export default defineAppModule({
  blades: pages,
  locales,
});

export * from "./composables";
