import assert from "node:assert/strict";
import test from "node:test";
import { createAssetEntriesLoader } from "../src/modules/asset-library/utilities/assetEntriesLoader";

test("asset loader forwards the restored offset, page size and server sort and batches only that page", async () => {
  const calls: unknown[][] = [];
  const page = Array.from({ length: 20 }, (_, index) => ({
    type: "blob" as const,
    name: `asset-${index + 80}.png`,
    relativeUrl: `/folder/asset-${index + 80}.png`,
  }));
  let references = 0;
  const loader = createAssetEntriesLoader({
    search: async (...args) => {
      calls.push(args);
      return { totalCount: 500, results: page };
    },
    loadReferences: async (entries) => {
      references++;
      assert.deepEqual(entries, page);
      return true;
    },
    apply: (result) => assert.equal(result.totalCount, 500),
    clear: () => undefined,
    onLoadingChange: () => undefined,
  });
  await loader.load({ folderUrl: "/folder", keyword: "asset", skip: 80, take: 20, sort: "name:desc" });
  assert.deepEqual(calls, [["/folder", "asset", { skip: 80, take: 20, sort: "name:desc" }]]);
  assert.equal(references, 1);
});

test("generated asset search client sends one authenticated POST and revives page dates", async () => {
  const { PageBuilderAssetSearchClient } = await import("../src/api_client/pagebuilderAssetSearch");
  let calls = 0;
  const client = new PageBuilderAssetSearchClient(undefined, {
    fetch: async (url, options) => {
      calls++;
      assert.equal(url, "/api/page-builder-assets/search");
      assert.equal(options?.method, "POST");
      assert.equal((options?.headers as Record<string, string>).authorization, "Bearer fixture-token");
      assert.deepEqual(JSON.parse(options?.body as string), {
        folderUrl: "/folder",
        skip: 80,
        take: 20,
        sort: "name:desc",
      });
      return new Response(
        JSON.stringify({
          totalCount: 500,
          results: [{ type: "blob", name: "asset-499.png", modifiedDate: "2026-10-06T00:00:00Z" }],
        }),
        { status: 200 },
      );
    },
  });
  client.setAuthToken("fixture-token");
  const result = await client.search({ folderUrl: "/folder", skip: 80, take: 20, sort: "name:desc" });
  assert.equal(result.totalCount, 500);
  assert.ok(result.results?.[0].modifiedDate instanceof Date);
  assert.equal(calls, 1);
});
