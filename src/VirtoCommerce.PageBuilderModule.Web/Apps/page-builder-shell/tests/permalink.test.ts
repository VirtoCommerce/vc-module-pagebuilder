import assert from "node:assert/strict";
import test from "node:test";
import * as pageBuilderLocales from "../src/modules/page-builder/locales";
import { isValidPermalink } from "../src/modules/page-builder/utilities/permalink";

test("isValidPermalink accepts URL paths, including Unicode and percent-encoded ones", () => {
  for (const permalink of [
    undefined,
    null,
    "",
    "/",
    "/test-page",
    "test-page",
    "/catalog/summer_sale.2026/",
    "/test%20page",
    "/страница",
    "/ページ",
  ]) {
    assert.equal(isValidPermalink(permalink), true, `${permalink} must be valid`);
  }
});

test("isValidPermalink rejects whitespace, reserved characters and non-canonical segments", () => {
  for (const permalink of [
    "/test page",
    " /test-page",
    "/test-page ",
    "/test\tpage",
    "/test page",
    "/test\npage",
    "/test\u0001page",
    "/test&page",
    "/test?x",
    "/test#y",
    "/test&page?x#y",
    "/test\\page",
    "//test",
    "/test//page",
    "/./test",
    "/test/..",
  ]) {
    assert.equal(isValidPermalink(permalink), false, `${JSON.stringify(permalink)} must be invalid`);
  }
});

test("every page builder locale has a permalink validation message", () => {
  for (const [localeCode, locale] of Object.entries(pageBuilderLocales)) {
    const message = (locale as { PAGE_BUILDER: { PAGES: { DETAILS: { VALIDATION?: { PERMALINK?: string } } } } })
      .PAGE_BUILDER.PAGES.DETAILS.VALIDATION?.PERMALINK;
    assert.ok(message, `${localeCode} must define PAGE_BUILDER.PAGES.DETAILS.VALIDATION.PERMALINK`);
  }
});
