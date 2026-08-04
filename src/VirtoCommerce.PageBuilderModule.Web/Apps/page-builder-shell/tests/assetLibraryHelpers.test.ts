import assert from "node:assert/strict";
import test from "node:test";
import * as assetLibraryLocales from "../src/modules/asset-library/locales";
import type { AssetEntry, AssetSearchResult } from "../src/modules/asset-library/types";
import { createAssetEntriesLoader } from "../src/modules/asset-library/utilities/assetEntriesLoader";
import {
  createAssetReferenceDetails,
  getReferenceSharedComponentNames,
  getReferencePageNames,
} from "../src/modules/asset-library/utilities/assetReferences";
import { getPreviewUrl, toPublicAssetUrl } from "../src/modules/asset-library/utilities/assetUrl";
import { createLatestRequestTracker } from "../src/utilities/latestRequest";

const origin = "https://admin.example.com";

interface Deferred<T> {
  promise: Promise<T>;
  resolve: (value: T) => void;
  reject: (reason?: unknown) => void;
}

function createDeferred<T>(): Deferred<T> {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((promiseResolve, promiseReject) => {
    resolve = promiseResolve;
    reject = promiseReject;
  });

  return { promise, resolve, reject };
}

function createAssetResult(name: string): AssetSearchResult {
  return {
    totalCount: 1,
    results: [{ type: "blob", name, relativeUrl: `/folder/${name}` }],
  };
}

test("asset entries loader applies only the newest search result and loading state", async () => {
  const searches = new Map<string, Deferred<AssetSearchResult>>();
  const referencesApplied: string[] = [];
  const entriesApplied: string[] = [];
  const loadingStates: boolean[] = [];
  const loader = createAssetEntriesLoader({
    search: (folderUrl) => {
      const request = createDeferred<AssetSearchResult>();
      searches.set(folderUrl, request);
      return request.promise;
    },
    loadReferences: async (entries, isCurrent) => {
      if (isCurrent()) {
        referencesApplied.push(entries[0].name);
        return true;
      }
      return false;
    },
    apply: (result) => {
      entriesApplied.push(result.results[0].name);
    },
    clear: () => undefined,
    onLoadingChange: (loading) => loadingStates.push(loading),
  });

  const firstLoad = loader.load({ folderUrl: "/first" });
  const secondLoad = loader.load({ folderUrl: "/second" });
  searches.get("/second")?.resolve(createAssetResult("second.png"));
  await secondLoad;
  searches.get("/first")?.resolve(createAssetResult("first.png"));
  await firstLoad;

  assert.deepEqual(referencesApplied, ["second.png"]);
  assert.deepEqual(entriesApplied, ["second.png"]);
  assert.deepEqual(loadingStates, [true, true, false]);
});

test("asset entries loader cannot apply a response that becomes stale during reference loading", async () => {
  const firstReferencesStarted = createDeferred<void>();
  const firstReferences = createDeferred<boolean>();
  const referencesApplied: string[] = [];
  const entriesApplied: string[] = [];
  const loader = createAssetEntriesLoader({
    search: async (folderUrl) => createAssetResult(folderUrl.slice(1) + ".png"),
    loadReferences: async (entries: AssetEntry[], isCurrent) => {
      if (entries[0].name === "first.png") {
        firstReferencesStarted.resolve();
        await firstReferences.promise;
      }

      if (isCurrent()) {
        referencesApplied.push(entries[0].name);
        return true;
      }
      return false;
    },
    apply: (result) => {
      entriesApplied.push(result.results[0].name);
    },
    clear: () => undefined,
    onLoadingChange: () => undefined,
  });

  const firstLoad = loader.load({ folderUrl: "/first" });
  await firstReferencesStarted.promise;
  const secondLoad = loader.load({ folderUrl: "/second" });
  await secondLoad;
  firstReferences.resolve(true);
  await firstLoad;

  assert.deepEqual(referencesApplied, ["second.png"]);
  assert.deepEqual(entriesApplied, ["second.png"]);
});

test("asset entries loader suppresses stale errors and propagates the current error", async () => {
  const searches = new Map<string, Deferred<AssetSearchResult>>();
  const loader = createAssetEntriesLoader({
    search: (folderUrl) => {
      const request = createDeferred<AssetSearchResult>();
      searches.set(folderUrl, request);
      return request.promise;
    },
    loadReferences: async (_entries, isCurrent) => isCurrent(),
    apply: () => undefined,
    clear: () => undefined,
    onLoadingChange: () => undefined,
  });

  const staleLoad = loader.load({ folderUrl: "/stale" });
  const currentLoad = loader.load({ folderUrl: "/current" });
  searches.get("/current")?.resolve(createAssetResult("current.png"));
  await currentLoad;
  searches.get("/stale")?.reject(new Error("stale failure"));
  await assert.doesNotReject(staleLoad);

  const failedLoad = loader.load({ folderUrl: "/failed" });
  searches.get("/failed")?.reject(new Error("current failure"));
  await assert.rejects(failedLoad, /current failure/);
});

test("latest request tracker keeps loading active until the newest request completes", () => {
  const pendingStates: boolean[] = [];
  const tracker = createLatestRequestTracker((pending) => pendingStates.push(pending));
  const first = tracker.begin();
  const second = tracker.begin();

  assert.equal(first.isCurrent(), false);
  assert.equal(second.isCurrent(), true);

  first.complete();
  assert.deepEqual(pendingStates, [true, true]);

  second.complete();
  assert.deepEqual(pendingStates, [true, true, false]);
});

test("latest request tracker invalidates pending work on dispose", () => {
  const pendingStates: boolean[] = [];
  const tracker = createLatestRequestTracker((pending) => pendingStates.push(pending));
  const request = tracker.begin();

  tracker.dispose();

  assert.equal(request.isCurrent(), false);
  request.complete();
  assert.deepEqual(pendingStates, [true, false]);
});

function flattenMessages(value: unknown, prefix = ""): Map<string, string> {
  const messages = new Map<string, string>();

  if (!value || typeof value !== "object" || Array.isArray(value)) {
    throw new TypeError(`Expected a locale object at ${prefix || "<root>"}.`);
  }

  for (const [key, child] of Object.entries(value)) {
    const path = prefix ? `${prefix}.${key}` : key;

    if (typeof child === "string") {
      messages.set(path, child);
    } else {
      for (const [childPath, message] of flattenMessages(child, path)) {
        messages.set(childPath, message);
      }
    }
  }

  return messages;
}

test("toPublicAssetUrl canonicalizes public asset paths", () => {
  assert.equal(
    toPublicAssetUrl("/assets/stores/B2B-store/Page%20Builder/hero.png", origin),
    "https://admin.example.com/assets/stores/B2B-store/Page%20Builder/hero.png",
  );
});

test("toPublicAssetUrl prefixes store-relative asset paths", () => {
  assert.equal(
    toPublicAssetUrl("/stores/B2B-store/Page%20Builder/hero.png", origin),
    "https://admin.example.com/assets/stores/B2B-store/Page%20Builder/hero.png",
  );
});

test("toPublicAssetUrl keeps custom public asset mount paths", () => {
  assert.equal(
    toPublicAssetUrl("/custom-public/assets/stores/B2B-store/Page%20Builder/hero.png", origin),
    "https://admin.example.com/custom-public/assets/stores/B2B-store/Page%20Builder/hero.png",
  );
});

test("toPublicAssetUrl handles unicode paths", () => {
  assert.equal(
    toPublicAssetUrl("/stores/B2B-store/Page Builder/снимок.png", origin),
    "https://admin.example.com/assets/stores/B2B-store/Page%20Builder/%D1%81%D0%BD%D0%B8%D0%BC%D0%BE%D0%BA.png",
  );
});

test("toPublicAssetUrl keeps absolute asset URLs unchanged", () => {
  assert.equal(
    toPublicAssetUrl("https://cdn.example.com/assets/stores/B2B-store/hero.png?size=large", origin),
    "https://cdn.example.com/assets/stores/B2B-store/hero.png?size=large",
  );
});

test("toPublicAssetUrl keeps external non-assets and data URLs unchanged", () => {
  assert.equal(
    toPublicAssetUrl("https://cdn.example.com/media/hero.png", origin),
    "https://cdn.example.com/media/hero.png",
  );
  assert.equal(toPublicAssetUrl("data:image/png;base64,abc", origin), "data:image/png;base64,abc");
});

test("getPreviewUrl prefers public entry URL over storage relative URL", () => {
  assert.equal(
    getPreviewUrl({
      type: "blob",
      name: "hero.png",
      url: "/custom-public/assets/stores/B2B-store/Page%20Builder/hero.png",
      relativeUrl: "/stores/B2B-store/Page%20Builder/hero.png",
      modifiedDate: "2026-06-18T20:12:47Z",
    }),
    "http://localhost/custom-public/assets/stores/B2B-store/Page%20Builder/hero.png?t=2026-06-18T20%3A12%3A47Z",
  );
});

test("asset used only by an unused shared component remains protected and visible", () => {
  const details = createAssetReferenceDetails([
    {
      assetUrl: "/stores/store-1/Page Builder/hero.png",
      referencesCount: 1,
      pageReferencesCount: 0,
      sharedComponentReferencesCount: 1,
      pages: [],
      sharedComponents: [{ id: "component-1", name: "Hero banner" }],
    },
  ]);

  assert.equal(details.referencesCount, 1);
  assert.equal(details.pageReferencesCount, 0);
  assert.equal(details.sharedComponentReferencesCount, 1);
  assert.deepEqual(getReferencePageNames(details.referencePages), []);
  assert.deepEqual(getReferenceSharedComponentNames(details.referenceSharedComponents), ["Hero banner"]);
});

test("asset used by both a page and a shared component keeps the combined preflight count and lists", () => {
  const details = createAssetReferenceDetails([
    {
      assetUrl: "/stores/store-1/Page Builder/hero.png",
      referencesCount: 2,
      pageReferencesCount: 1,
      sharedComponentReferencesCount: 1,
      pages: [{ id: "page-1", name: "Homepage" }],
      sharedComponents: [{ id: "component-1", name: "Hero banner" }],
    },
  ]);

  assert.equal(details.referencesCount, 2);
  assert.equal(details.pageReferencesCount, 1);
  assert.equal(details.sharedComponentReferencesCount, 1);
  assert.deepEqual(getReferencePageNames(details.referencePages), ["Homepage"]);
  assert.deepEqual(getReferenceSharedComponentNames(details.referenceSharedComponents), ["Hero banner"]);
});

test("asset reference details preserve the aggregate count when some shared components are hidden", () => {
  const details = createAssetReferenceDetails([
    {
      assetUrl: "/stores/store-1/Page Builder/hero.png",
      referencesCount: 6,
      pageReferencesCount: 4,
      sharedComponentReferencesCount: 2,
      pages: [
        { id: "page-1", name: "Homepage" },
        { id: "page-2", name: "Catalog" },
        { id: "page-3", name: "Checkout" },
        { id: "page-4", name: "Campaign" },
      ],
      sharedComponents: [{ id: "component-1", name: "Visible component" }],
    },
  ]);

  assert.equal(details.referencesCount, 6);
  assert.equal(details.pageReferencesCount, 4);
  assert.equal(details.sharedComponentReferencesCount, 1);
  assert.deepEqual(getReferenceSharedComponentNames(details.referenceSharedComponents), ["Visible component"]);
});

test("asset library locale keys and placeholders match English", () => {
  const englishMessages = flattenMessages(assetLibraryLocales.en);
  const englishKeys = [...englishMessages.keys()].sort();

  for (const [localeCode, locale] of Object.entries(assetLibraryLocales)) {
    const messages = flattenMessages(locale);
    assert.deepEqual([...messages.keys()].sort(), englishKeys, `${localeCode} locale keys differ from English`);

    for (const key of englishKeys) {
      const placeholders = [...(messages.get(key) ?? "").matchAll(/\{([^{}]+)\}/g)].map((match) => match[1]).sort();
      const englishPlaceholders = [...(englishMessages.get(key) ?? "").matchAll(/\{([^{}]+)\}/g)]
        .map((match) => match[1])
        .sort();
      assert.deepEqual(placeholders, englishPlaceholders, `${localeCode}.${key} placeholders differ from English`);
    }
  }
});
