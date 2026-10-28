import assert from "node:assert/strict";
import test from "node:test";
import {
  ApiException,
  PageBuilderPermalinkConflict,
  type PageBuilderPermalinkUsage,
} from "../src/api_client/virtocommerce.pagebuildermodule";
import * as pageBuilderLocales from "../src/modules/page-builder/locales";
import {
  canUnpublishAll,
  formatPageNames,
  getConflictsOfRejectedPublish,
  getPublishConflicts,
  getUsagesInLanguage,
} from "../src/modules/page-builder/utilities/permalinkUsages";

const usage = (overrides: PageBuilderPermalinkUsage): PageBuilderPermalinkUsage => ({
  groupId: "group",
  name: "Page",
  conflict: PageBuilderPermalinkConflict.None,
  canUnpublish: true,
  ...overrides,
});

test("getPublishConflicts splits blocking and possible conflicts and ignores the rest", () => {
  const certain = usage({ groupId: "certain", conflict: PageBuilderPermalinkConflict.Certain });
  const possible = usage({ groupId: "possible", conflict: PageBuilderPermalinkConflict.Possible });
  const draft = usage({ groupId: "draft" });

  const conflicts = getPublishConflicts([certain, possible, draft]);

  assert.deepEqual(conflicts.blocking, [certain]);
  assert.deepEqual(conflicts.possible, [possible]);
  assert.deepEqual(getPublishConflicts(undefined), { blocking: [], possible: [] });
});

test("canUnpublishAll is false when a conflicting page has unpublished changes", () => {
  assert.equal(canUnpublishAll([usage({}), usage({ canUnpublish: false })]), false);
  assert.equal(canUnpublishAll([usage({}), usage({})]), true);
});

test("getUsagesInLanguage keeps pages in the same language or in any language", () => {
  const usages = [
    usage({ groupId: "same", cultureName: "en-US" }),
    usage({ groupId: "case", cultureName: "EN-us" }),
    usage({ groupId: "any", cultureName: undefined }),
    usage({ groupId: "other", cultureName: "de-DE" }),
  ];

  assert.deepEqual(
    getUsagesInLanguage(usages, "en-US").map((x) => x.groupId),
    ["same", "case", "any"],
  );
  assert.equal(getUsagesInLanguage(usages, undefined).length, 4);
});

test("formatPageNames quotes names and falls back to the id", () => {
  assert.equal(formatPageNames([usage({ name: "About" }), usage({ name: "", groupId: "g2" })]), '"About", "g2"');
});

test("getConflictsOfRejectedPublish reads the pages of a 409 and ignores other errors", () => {
  const body = JSON.stringify({ error: "conflict", conflicts: [{ groupId: "old", name: "About" }] });

  assert.deepEqual(getConflictsOfRejectedPublish(new ApiException("", 409, body, {}, null)), [
    { groupId: "old", name: "About" },
  ]);
  assert.deepEqual(getConflictsOfRejectedPublish(new ApiException("", 409, "not json", {}, null)), []);
  assert.equal(getConflictsOfRejectedPublish(new ApiException("", 400, body, {}, null)), null);
  assert.equal(getConflictsOfRejectedPublish(new Error("network")), null);
});

test("every page builder locale has the permalink usage and publish conflict messages", () => {
  for (const [localeCode, locale] of Object.entries(pageBuilderLocales)) {
    const pages = (locale as { PAGE_BUILDER: { PAGES: Record<string, Record<string, unknown>> } }).PAGE_BUILDER.PAGES;
    const warnings = pages.DETAILS.WARNINGS as Record<string, string> | undefined;
    assert.match(warnings?.PERMALINK_USED ?? "", /\{pages\}/, `${localeCode} PERMALINK_USED`);
    for (const key of [
      "PUBLISH_CONFLICT",
      "PUBLISH_CONFLICT_HAS_CHANGES",
      "PUBLISH_POSSIBLE_CONFLICT",
      "PUBLISH_CONFLICT_CHANGED",
    ]) {
      assert.match(String(pages.ALERTS[key] ?? ""), /\{pages\}/, `${localeCode} ${key}`);
    }
  }
});
