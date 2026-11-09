import { i18n } from "@vc-shell/framework";
import { defineRule } from "vee-validate";
import { isValidPermalink } from "./utilities/permalink";

export function definePermalinkRule() {
  defineRule("permalink", (value: string) =>
    isValidPermalink(value) ? true : i18n.global.t("PAGE_BUILDER.PAGES.DETAILS.VALIDATION.PERMALINK"),
  );
}
