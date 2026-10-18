import assert from "node:assert/strict";
import test from "node:test";
import { groupPath, historyFiles, historyPath, mergeSaves, rulesPath, savesHistory, themeHistoryUrl } from "../src/modules/designer/history";
import { templatePath } from "../src/modules/designer/templates";
import type { PageVersion } from "../src/modules/designer/api";

const at = (minute: number) => `2026-10-18T10:${String(minute).padStart(2, "0")}:00Z`;
const change = (sha: string, minute: number, saveId?: string, published = false, message = "Saved"): PageVersion => ({
  sha,
  shortSha: sha,
  date: at(minute),
  message,
  saveId,
  published,
});

test("a template's history covers what its Save writes: the template, its rules, the header and footer", () => {
  assert.deepEqual(historyFiles({ versioned: true, rules: true, groups: true }), ["page", "rules", "header", "footer"]);
  // a page has no rules; a theme without a header and footer has none to show
  assert.deepEqual(historyFiles({ versioned: true, rules: false, groups: true }), ["page", "header", "footer"]);
  // a site that keeps no versions: only the page's own (a file page in git)
  assert.deepEqual(historyFiles({ versioned: false, rules: true, groups: true }), ["page"]);
});

test("each file of the history is a theme file, but a database page's own", () => {
  const template = templatePath("default", "product.printer");
  assert.equal(historyPath("page", "default", template), "default/templates/product.printer.json");
  assert.equal(historyPath("rules", "default", template), "default/config/template_rules.json");
  assert.equal(historyPath("header", "default"), "default/sections/header-group.json");
  assert.equal(historyPath("footer", "default"), "default/sections/footer-group.json");
  assert.equal(historyPath("page", "default"), undefined);
  assert.equal(historyPath("rules", undefined), undefined);
  assert.equal(rulesPath("dark"), "dark/config/template_rules.json");
  assert.equal(groupPath("dark", "header"), "dark/sections/header-group.json");
});

test("a theme file's history is asked for by store and path; a version by its sha, restored under the one picked", () => {
  const path = "default/templates/product.json";
  assert.equal(themeHistoryUrl("B2B-store", path), "/api/pagebuilder/theme-history?storeId=B2B-store&path=default%2Ftemplates%2Fproduct.json");
  assert.equal(themeHistoryUrl("s1", path, "1c0ffee"), "/api/pagebuilder/theme-history/1c0ffee?storeId=s1&path=default%2Ftemplates%2Fproduct.json");
  assert.equal(
    themeHistoryUrl("s1", path, "1c0ffee", "restore", { version: "abcdef1" }),
    "/api/pagebuilder/theme-history/1c0ffee/restore?storeId=s1&path=default%2Ftemplates%2Fproduct.json&version=abcdef1",
  );
});

test("the changes of one Save are one version, holding each file's change, named by its newest", () => {
  const saves = mergeSaves([
    { file: "page", versions: [change("t2", 30, "s2"), change("t1", 20, "s1", true)] },
    { file: "header", versions: [change("h2", 31, "s2", false, "Restored t1"), change("h1", 21, "s1", true)] },
    // made before saves had an id: a version of its own
    { file: "footer", versions: [change("f0", 10)] },
  ]);

  assert.deepEqual(
    saves.map((x) => [x.id, x.sha, x.message, x.files?.map((f) => `${f.file}:${f.sha}`).join(" ")]),
    [
      ["s2", "h2", "Restored t1", "page:t2 header:h2"],
      ["s1", "h1", "Saved", "page:t1 header:h1"],
      ["f0", "f0", "Saved", "footer:f0"],
    ],
  );
  // published: every file's change it holds is live
  assert.deepEqual(saves.map((x) => x.published), [false, true, false]);
});

test("a Save is published only when all it changed is live", () => {
  const [save] = mergeSaves([
    { file: "page", versions: [change("t1", 20, "s1", true)] },
    { file: "header", versions: [change("h1", 20, "s1", false)] },
  ]);
  assert.equal(save.published, false);
});

test("where a file has older versions to read, the list stops at its oldest read: no Save shows half of its files", () => {
  const saves = mergeSaves([
    { file: "page", versions: [change("t3", 30, "s3"), change("t2", 20, "s2")], hasMore: true },
    { file: "header", versions: [change("h3", 30, "s3"), change("h2", 20, "s2"), change("h1", 5, "s1")] },
  ]);
  assert.deepEqual(saves.map((x) => x.id), ["s3", "s2"]);
});

test("Show older reads on where each file's history stopped, and the Save it split comes together", async () => {
  const pages: Record<string, Record<string, { versions: PageVersion[]; hasMore?: boolean; endCursor?: string }>> = {
    page: {
      "": { versions: [change("t2", 20, "s2"), change("t1", 10, "s1")], hasMore: true, endCursor: "p1" },
      p1: { versions: [change("t0", 1, "s0")] },
    },
    header: {
      "": { versions: [change("h1", 10, "s1"), change("h0", 1, "s0")] },
    },
  };
  const asked: string[] = [];
  const read = savesHistory(
    (["page", "header"] as const).map((file) => ({
      file,
      read: async (after?: string) => {
        asked.push(`${file}:${after ?? ""}`);
        return pages[file][after ?? ""];
      },
    })),
  );

  const first = await read();
  // s0 has an older page change still to read: it waits
  assert.deepEqual(first.versions.map((x) => x.id), ["s2", "s1"]);
  assert.equal(first.hasMore, true);

  const all = await read(true);
  assert.deepEqual(all.versions.map((x) => `${x.id}:${x.files?.map((f) => f.file).join("+")}`), ["s2:page", "s1:page+header", "s0:page+header"]);
  assert.equal(all.hasMore, false);
  // the header had no more: only the page was read on
  assert.deepEqual(asked, ["page:", "header:", "page:p1"]);
});
