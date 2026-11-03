import * as pages from "./pages";
import * as locales from "./locales";
import { defineAppModule, i18n } from "@vc-shell/framework";
import { defineRule } from "vee-validate";
import { isValidPermalink } from "./utilities/permalink";

defineRule("permalink", (value: string) =>
  isValidPermalink(value) ? true : i18n.global.t("PAGE_BUILDER.PAGES.DETAILS.VALIDATION.PERMALINK"),
);

export default defineAppModule({
  blades: pages,
  locales,
});

export * from "./composables";
