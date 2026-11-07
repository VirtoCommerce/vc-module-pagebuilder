import assert from "node:assert/strict";
import test from "node:test";
import {
  THEME_ONLY_FILES,
  fromGitVersion,
  groupPath,
  historyItems,
  historyOffer,
  historyStatusUrl,
  historyUrl,
  parseHistoryPage,
  parseHistoryStatus,
  restoreUrl,
  rulesPath,
  settingsPath,
  versionUrl,
} from "../src/modules/designer/history";
import { templatePath } from "../src/modules/designer/templates";

const url = (value: string) => new URL(value, "https://admin.example");

test("the theme files of the history are named with their theme folder", () => {
  assert.equal(rulesPath("dark"), "dark/config/template_rules.json");
  assert.equal(groupPath("dark", "header"), "dark/sections/header-group.json");
  assert.equal(settingsPath("dark"), "dark/config/settings_data.json");
});

test("a database page's history covers what its Save writes: the page, the header and footer, the theme settings", () => {
  assert.deepEqual(historyItems({ groupId: "g1", theme: "default", rules: false, groups: true, settings: true }), [
    { file: "page", item: "page:g1" },
    { file: "header", item: "theme:default/sections/header-group.json" },
    { file: "footer", item: "theme:default/sections/footer-group.json" },
    { file: "settings", item: "theme:default/config/settings_data.json" },
  ]);
});

test("with the Theme tab loaded, the history covers the brand and both palettes, which a version restores, not previews", () => {
  assert.deepEqual(
    historyItems({ groupId: "g1", theme: "default", rules: false, groups: false, settings: false, look: true }).map((x) => [x.file, x.item]),
    [
      ["page", "page:g1"],
      ["brand", "theme:default/config/brand.json"],
      ["palette", "theme:default/config/palette.json"],
      ["darkPalette", "theme:default/config/palette.dark.json"],
    ],
  );
  assert.deepEqual(THEME_ONLY_FILES, ["rules", "settings", "brand", "palette", "darkPalette"]);
});

test("a template is a theme file of the history, with its rules", () => {
  const template = templatePath("default", "product.printer");
  assert.deepEqual(historyItems({ templatePath: template, theme: "default", rules: true, groups: false, settings: false }), [
    { file: "page", item: "theme:default/templates/product.printer.json" },
    { file: "rules", item: "theme:default/config/template_rules.json" },
  ]);
  // without the theme known, only the page's own
  assert.deepEqual(historyItems({ groupId: "g1", rules: true, groups: true, settings: true }), [{ file: "page", item: "page:g1" }]);
});

test("the history is asked for by store and items; a version by its id, restored for the items it changed", () => {
  const list = url(historyUrl("B2B-store", ["page:g1", "theme:default/sections/header-group.json"], "c1"));
  assert.equal(list.pathname, "/api/pagebuilder/history");
  assert.deepEqual(list.searchParams.getAll("item"), ["page:g1", "theme:default/sections/header-group.json"]);
  assert.equal(list.searchParams.get("cursor"), "c1");
  assert.equal(url(historyUrl("s1", ["page:g1"])).searchParams.has("cursor"), false);

  const version = url(versionUrl("s1", "HA_v-Hs8f7RVPw9gHHqUAxmPDJ8", "page:g1"));
  assert.equal(version.pathname, "/api/pagebuilder/history/HA_v-Hs8f7RVPw9gHHqUAxmPDJ8");
  assert.equal(version.searchParams.get("item"), "page:g1");

  const restore = url(restoreUrl("s1", "HA_v-Hs8f7RVPw9gHHqUAxmPDJ8", ["page:g1", "theme:default/config/template_rules.json"]));
  assert.equal(restore.pathname, "/api/pagebuilder/history/HA_v-Hs8f7RVPw9gHHqUAxmPDJ8/restore");
  assert.deepEqual(restore.searchParams.getAll("item"), ["page:g1", "theme:default/config/template_rules.json"]);

  assert.equal(historyStatusUrl("s1"), "/api/pagebuilder/history/status?storeId=s1");
});

test("a page of the history comes merged from the server: each version once, with the items it changed", () => {
  const page = parseHistoryPage({
    status: "enabled",
    cursor: "next",
    versions: [
      {
        versionId: "HA_v-Hs8f7RVPw9gHHqUAxmPDJ8",
        date: "2026-11-06T12:00:00Z",
        description: "Saved",
        saveId: "s1",
        published: true,
        author: { name: "alice", login: "alice-gh" },
        items: ["page:g1", "theme:default/sections/header-group.json"],
      },
      { versionId: 7 },
      null,
    ],
  });

  assert.equal(page.status, "enabled");
  assert.equal(page.cursor, "next");
  assert.deepEqual(page.versions, [
    {
      id: "HA_v-Hs8f7RVPw9gHHqUAxmPDJ8",
      label: "HA_v-Hs",
      date: "2026-11-06T12:00:00Z",
      description: "Saved",
      author: { name: "alice", login: "alice-gh" },
      published: true,
      items: ["page:g1", "theme:default/sections/header-group.json"],
    },
  ]);
  assert.deepEqual(parseHistoryPage({ status: "setupRequired" }), { status: "setupRequired", versions: [], cursor: undefined });
  assert.equal(parseHistoryPage(undefined).status, "unavailable");
});

test("a status the designer doesn't know reads as unavailable", () => {
  assert.equal(parseHistoryStatus("enabled"), "enabled");
  assert.equal(parseHistoryStatus("setupRequired"), "setupRequired");
  assert.equal(parseHistoryStatus("Enabled"), "unavailable");
  assert.equal(parseHistoryStatus(undefined), "unavailable");
});

test("a file page's git version is listed like a version of the site's history, by its sha", () => {
  assert.deepEqual(
    fromGitVersion({ sha: "1c0ffee1c0ffee", shortSha: "1c0ffee", date: "d", message: "Edited", vcUser: "alice", author: { name: "A", login: "a" }, published: true, mine: true }),
    { id: "1c0ffee1c0ffee", label: "1c0ffee", date: "d", description: "Edited", author: { name: "alice", login: "a" }, published: true, mine: true },
  );
  assert.equal(fromGitVersion({ sha: "abcdef0123456789" }).label, "abcdef0");
});

test("history is offered by what is open: git for a file page, the site's status for a database page or a template", () => {
  const offer = (open: Partial<Parameters<typeof historyOffer>[0]>) =>
    historyOffer({ shared: false, filePage: false, gitFlow: false, site: false, ...open });

  assert.equal(offer({ filePage: true, gitFlow: true }), "history");
  assert.equal(offer({ filePage: true }), "enable");
  assert.equal(offer({ site: true, status: "enabled" }), "history");
  // setting it up is the site panel's job (PageBuilderGit)
  assert.equal(offer({ site: true, status: "setupRequired" }), "enable");
  assert.equal(offer({ site: true, status: "unavailable" }), "none");
  // not known yet
  assert.equal(offer({ site: true }), "none");
  assert.equal(offer({ shared: true, site: true, status: "enabled" }), "none");
});
