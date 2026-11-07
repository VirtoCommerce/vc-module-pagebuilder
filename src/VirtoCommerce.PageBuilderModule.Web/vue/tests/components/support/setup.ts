import { afterEach, beforeEach } from "vitest";
import { config } from "@vue/test-utils";
import { createI18n } from "vue-i18n";
import locales from "../../../src/modules/locales";
import { resetVcShell } from "./vcShell";

// the designer's English messages, so that the tests read what the editor reads
config.global.plugins = [createI18n({ legacy: false, locale: "en", fallbackLocale: "en", missingWarn: false, fallbackWarn: false, messages: { en: locales.en as never } })];

beforeEach(resetVcShell);
afterEach(() => {
  document.body.innerHTML = "";
});
