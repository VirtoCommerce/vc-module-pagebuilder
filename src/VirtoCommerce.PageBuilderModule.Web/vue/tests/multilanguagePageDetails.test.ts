import assert from "node:assert/strict";
import test from "node:test";
import { isFieldOverridden } from "../src/modules/page-builder/composables/useMultilanguageInheritance";
import {
  getLanguagePillText,
  hasLanguageVersions,
  isMultilanguageRowArchived,
  isPermalinkCheckNeeded,
  summarizeBatchResult,
  summarizeLanguageStatuses,
} from "../src/modules/page-builder/composables/useMultilanguagePageDetails/helpers";

test("isMultilanguageRowArchived needs at least one version, all archived", () => {
  assert.equal(isMultilanguageRowArchived([]), false);
  assert.equal(isMultilanguageRowArchived(undefined), false);
  assert.equal(isMultilanguageRowArchived([{ cultureName: "en-US", status: "Missing" }]), false);
  assert.equal(
    isMultilanguageRowArchived([
      { cultureName: "en-US", status: "Archived", page: { id: "en" } },
      { cultureName: "nl-NL", status: "Missing" },
    ]),
    true,
  );
  assert.equal(
    isMultilanguageRowArchived([
      { cultureName: "en-US", status: "Archived", page: { id: "en" } },
      { cultureName: "nl-NL", status: "Draft", page: { id: "nl" } },
    ]),
    false,
  );
});

test("hasLanguageVersions: only a root without versions can be deleted, others are archived", () => {
  assert.equal(hasLanguageVersions(undefined), false);
  assert.equal(hasLanguageVersions([{ cultureName: "en-US", status: "Missing" }]), false);
  assert.equal(hasLanguageVersions([{ cultureName: "en-US", status: "Archived", page: { id: "en" } }]), true);
});

test("isFieldOverridden reads the mask like the backend, with spaces", () => {
  assert.equal(isFieldOverridden("Permalink, EndDate", "EndDate"), true);
  assert.equal(isFieldOverridden("Permalink", "EndDate"), false);
  assert.equal(isFieldOverridden(undefined, "Permalink"), false);
});

test("isPermalinkCheckNeeded only for a new page or a changed permalink or language", () => {
  const saved = { permalink: "/faq", cultureName: "en-US" };

  assert.equal(isPermalinkCheckNeeded(undefined, saved), true);
  assert.equal(isPermalinkCheckNeeded(saved, { permalink: "/faq", cultureName: "en-US" }), false);
  assert.equal(isPermalinkCheckNeeded(saved, { permalink: "faq", cultureName: "EN-us" }), false);
  assert.equal(isPermalinkCheckNeeded(saved, { permalink: "/help", cultureName: "en-US" }), true);
  assert.equal(isPermalinkCheckNeeded(saved, { permalink: "/faq", cultureName: "nl-NL" }), true);
});

test("summarizeBatchResult reports failed languages apart from skipped ones", () => {
  assert.deepEqual(
    summarizeBatchResult({
      processedCultureNames: ["en-US"],
      skipped: [
        { cultureName: "de-DE", reason: "NoDraft" },
        { cultureName: "nl-NL", reason: "Failed" },
      ],
    }),
    { processed: "en-US", skipped: "de-DE", failed: "nl-NL" },
  );
  assert.deepEqual(summarizeBatchResult({}), { processed: "—", skipped: "—", failed: "" });
});

test("getLanguagePillText shows the region only where the language part is ambiguous", () => {
  const cultures = ["en-US", "en-GB", "de-DE", "nl-NL", "nl-BL"];
  assert.deepEqual(
    cultures.map((x) => getLanguagePillText(x, cultures)),
    ["en-US", "en-GB", "DE", "nl-NL", "nl-BL"],
  );
  assert.equal(getLanguagePillText("en-GB", ["en-GB"]), "en-GB");
  assert.equal(getLanguagePillText(undefined, [undefined]), "");
});

test("summarizeLanguageStatuses counts published and outdated languages", () => {
  assert.deepEqual(summarizeLanguageStatuses(undefined), { published: 0, total: 0, outdated: 0 });
  assert.deepEqual(
    summarizeLanguageStatuses([
      { cultureName: "en-US", status: "Published", page: { id: "en" } },
      { cultureName: "nl-NL", status: "Published", isOutdated: true, page: { id: "nl" } },
      { cultureName: "de-DE", status: "Draft", page: { id: "de" } },
      { cultureName: "fr-FR", status: "Missing" },
    ]),
    { published: 2, total: 4, outdated: 1 },
  );
});
