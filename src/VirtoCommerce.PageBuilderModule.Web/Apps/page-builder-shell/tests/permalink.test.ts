import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import test from "node:test";
import * as pageBuilderLocales from "../src/modules/page-builder/locales";
import { getPermalinkRules, isValidPermalink } from "../src/modules/page-builder/utilities/permalink";

// Cases shared with the .NET tests, so both implementations of the permalink rule check the same values.
const cases = JSON.parse(
  readFileSync(resolve(__dirname, "../../../../../tests/TestData/permalink-cases.json"), "utf8"),
) as { valid: (string | null)[]; invalid: string[] };

test("isValidPermalink accepts the shared valid cases", () => {
  for (const permalink of [undefined, ...cases.valid]) {
    assert.equal(isValidPermalink(permalink), true, `${JSON.stringify(permalink)} must be valid`);
  }
});

test("isValidPermalink rejects the shared invalid cases", () => {
  for (const permalink of cases.invalid) {
    assert.equal(isValidPermalink(permalink), false, `${JSON.stringify(permalink)} must be invalid`);
  }
});

test("getPermalinkRules does not check a stored permalink, so it does not block saving other fields", () => {
  assert.equal(getPermalinkRules("/legacy page", "/legacy page"), "required");
});

test("getPermalinkRules checks a changed permalink and a new page's permalink", () => {
  assert.equal(getPermalinkRules("/legacy page 2", "/legacy page"), "required|permalink");
  assert.equal(getPermalinkRules("/test page", undefined), "required|permalink");
  assert.equal(getPermalinkRules(undefined, undefined), "required|permalink");
});

test("every page builder locale has a permalink validation message", () => {
  for (const [localeCode, locale] of Object.entries(pageBuilderLocales)) {
    const message = (locale as { PAGE_BUILDER: { PAGES: { DETAILS: { VALIDATION?: { PERMALINK?: string } } } } })
      .PAGE_BUILDER.PAGES.DETAILS.VALIDATION?.PERMALINK;
    assert.ok(message, `${localeCode} must define PAGE_BUILDER.PAGES.DETAILS.VALIDATION.PERMALINK`);
  }
});
