import assert from "node:assert/strict";
import test from "node:test";
import { isFieldOverridden } from "../src/modules/page-builder/composables/useMultilanguageInheritance";
import {
  hasLanguageVersions,
  isMultilanguageRowArchived,
  isPermalinkCheckNeeded,
  summarizeBatchResult,
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
