import assert from "node:assert/strict";
import test from "node:test";
import useUrlParams from "../src/modules/page-builder/composables/useStoreParams";

function setStoreSearch(storeId: string | null) {
  globalThis.window = {
    location: {
      search: storeId ? `?storeId=${storeId}` : "",
    },
  } as Window & typeof globalThis;
}

test("validateStoreContext treats 200 null store response as not found", async () => {
  setStoreSearch("INVALID-STORE-ID-9999");
  globalThis.fetch = async () => ({
    ok: true,
    status: 200,
    json: async () => null,
  }) as Response;

  const storeContext = useUrlParams();
  storeContext.initUrlParams();

  assert.equal(await storeContext.validateStoreContext(), false);
  assert.equal(storeContext.storeContextStatus.value, "notFound");
});

test("validateStoreContext marks a valid store as ready", async () => {
  setStoreSearch("B2B-store");
  globalThis.fetch = async () => ({
    ok: true,
    status: 200,
    json: async () => ({
      url: "https://example.com",
      languages: ["en-US"],
    }),
  }) as Response;

  const storeContext = useUrlParams();
  storeContext.initUrlParams();

  assert.equal(await storeContext.validateStoreContext(), true);
  assert.equal(storeContext.storeContextStatus.value, "ready");
});

// keep last: a pinned store stays pinned for the module's lifetime
test("setStoreId pins the store over the URL and uses the store it is given", async () => {
  setStoreSearch("B2B-store");
  globalThis.fetch = async () => {
    throw new Error("the given store must not be fetched");
  };

  const storeContext = useUrlParams();
  storeContext.setStoreId("Electronics", { url: "https://electronics.example.com", languages: ["en-US", "de-DE"] });
  storeContext.initUrlParams();

  assert.equal(storeContext.storeId.value, "Electronics");
  assert.equal(await storeContext.validateStoreContext(), true);
  assert.deepEqual(await storeContext.getLanguages(), ["en-US", "de-DE"]);
  assert.equal(storeContext.storeContextStatus.value, "ready");
});
