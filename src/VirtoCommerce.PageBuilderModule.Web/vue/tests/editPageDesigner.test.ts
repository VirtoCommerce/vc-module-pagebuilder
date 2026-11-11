import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import test from "node:test";
import { runInNewContext } from "node:vm";

const source = readFileSync(resolve(__dirname, "../../Scripts/blades/pages/edit-page.js"), "utf8");
const NEW_PAGE_BUILDER = "VirtoCommerce.PageBuilderModule.Store.NewPageBuilder";
const ANGULAR_DESIGNER = "https://admin.test/Modules/$(VirtoCommerce.PageBuilderModule)/Content/page-builder-designer/index.html";
const NEW_DESIGNER = "https://admin.test/apps/page-builder-shell/";

const store = (newPageBuilder: boolean) => ({ settings: [{ name: NEW_PAGE_BUILDER, value: newPageBuilder }] });
const unreadable = { status: 500 };

const settled = () => new Promise((resolved) => setTimeout(resolved));

const anything: unknown = new Proxy(function () {}, {
  get: (_target, key) => (key === Symbol.toPrimitive ? () => "" : anything),
  apply: () => anything,
});

/** Each read of the store takes the next answer. */
function openPage(answers: unknown[]) {
  const opened: string[] = [];
  const errors: string[] = [];
  let reads = 0;
  const stores = {
    get: () => {
      const answer = answers[reads++];
      return { $promise: answer === unreadable ? Promise.reject(answer) : Promise.resolve(answer) };
    },
  };
  const blade: Record<string, unknown> = { storeId: "B2B", contentType: "pages", toolbarCommands: [], parentBlade: {}, currentEntity: { name: "a.json", relativeUrl: "/a.json" } };
  const $scope = { blade, $watch: () => undefined, $on: () => undefined };
  const pageBuilderApi = {
    getPage: (_query: unknown, success: (data: unknown) => void) => success({ data: JSON.stringify([{ permalink: "/a" }]) }),
    publishStatus: () => undefined,
    legacyDraft: () => undefined,
  };
  const bladeNavigationService = { setError: (message: string) => errors.push(message) };

  let controller: (...deps: unknown[]) => void = () => undefined;
  const angular = {
    module: () => ({
      controller: (_name: string, definition: unknown[]) => {
        controller = definition.at(-1) as typeof controller;
      },
    }),
    copy: (value: unknown) => JSON.parse(JSON.stringify(value)),
    noop: () => undefined,
  };
  const window = { location: { pathname: "/", origin: "https://admin.test" }, open: (url: string) => opened.push(url) };
  const _ = { find: <T>(items: T[], predicate: (item: T) => boolean) => items.find(predicate) };
  runInNewContext(source, { angular, window, _, console, setTimeout, clearTimeout });

  const $q = { resolve: (value: unknown) => Promise.resolve(value) };
  controller(
    anything, $scope, $q, anything, { indexedSearchEnabled: () => undefined }, pageBuilderApi, bladeNavigationService,
    anything, anything, anything, anything, anything, anything, () => ({}), anything, stores,
  );

  const command = () => (blade.toolbarCommands as { name: string; executeMethod: () => void }[]).find((x) => x.name === "pageBuilder.commands.open-designer")!;
  return {
    opened,
    errors,
    reads: () => reads,
    press: async () => {
      command().executeMethod();
      await settled();
    },
  };
}

test("the Designer command opens the designer the store is on", async () => {
  for (const [newPageBuilder, designer] of [[false, ANGULAR_DESIGNER], [true, NEW_DESIGNER]] as const) {
    const page = openPage([store(newPageBuilder)]);
    await page.press();
    assert.equal(page.opened.length, 1);
    assert.ok(page.opened[0].startsWith(`${designer}?`), page.opened[0]);
    assert.equal(page.reads(), 1);
  }
});

test("a failed store read opens no designer and is reported", async () => {
  const page = openPage([unreadable, unreadable]);
  await page.press();
  assert.deepEqual(page.opened, []);
  assert.ok(page.errors.includes("Error 500"));
});

test("the Designer command reads the store again after a failed read", async () => {
  const page = openPage([unreadable, store(true)]);
  await page.press();
  assert.equal(page.reads(), 2);
  assert.equal(page.opened.length, 1);
  assert.ok(page.opened[0].startsWith(`${NEW_DESIGNER}?`), page.opened[0]);
});

test("each press after failed reads asks the store once more", async () => {
  const page = openPage([unreadable, unreadable, store(false)]);
  await page.press();
  assert.deepEqual(page.opened, []);
  await page.press();
  assert.equal(page.reads(), 3);
  assert.equal(page.opened.length, 1);
  assert.ok(page.opened[0].startsWith(`${ANGULAR_DESIGNER}?`), page.opened[0]);
});
