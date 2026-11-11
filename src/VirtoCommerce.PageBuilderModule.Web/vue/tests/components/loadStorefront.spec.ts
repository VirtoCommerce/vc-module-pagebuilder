import { beforeEach, describe, expect, it } from "vitest";
import { loadPreviewUsers, loadStorefront, storeTheme } from "../../src/modules/designer/api";
import { fakePlatform, status, type FakePlatform } from "./support/fakePlatform";

const STORE = "B2B store";

describe("loadStorefront", () => {
  let platform: FakePlatform;

  beforeEach(() => {
    platform = fakePlatform().install();
    platform.on("GET", "/api/stores/B2B%20store", {
      url: "http://ignored.test",
      secureUrl: "https://ignored.test",
      defaultLanguage: "de-DE",
      defaultCurrency: "EUR",
      languages: ["de-DE", 1, "en-US"],
    });
  });

  it("takes the storefront from the designer endpoint, and the catalog context and languages from the store", async () => {
    platform.on("GET", "/api/pagebuilder/designer/storefront", { url: "https://shop.test", previewPath: "/custom-preview" });

    await expect(loadStorefront(STORE)).resolves.toEqual({
      url: "https://shop.test",
      previewPath: "/custom-preview",
      catalog: { storeId: STORE, cultureName: "de-DE", currencyCode: "EUR" },
      languages: ["de-DE", "en-US"],
    });
    const [request] = platform.to("/api/pagebuilder/designer/storefront");
    expect(request.query).toEqual({ storeId: STORE });
    expect(platform.requests.some((x) => x.path.startsWith("/api/platform/settings"))).toBe(false);
  });

  it("has no URL when the server knows no storefront", async () => {
    platform.on("GET", "/api/pagebuilder/designer/storefront", { url: null, previewPath: "/designer-preview" });

    expect((await loadStorefront(STORE)).url).toBeUndefined();
  });

  it("reads the store once for the parts that ask at the same time, and again after", async () => {
    platform.on("GET", "/api/pagebuilder/designer/storefront", { url: "https://shop.test", previewPath: "/designer-preview" });

    await Promise.all([loadStorefront(STORE), storeTheme(STORE), loadPreviewUsers(STORE)]);
    expect(platform.to("/api/stores/B2B%20store")).toHaveLength(1);

    await storeTheme(STORE);
    expect(platform.to("/api/stores/B2B%20store")).toHaveLength(2);
  });

  it("keeps the storefront when the store cannot be read, with the default catalog context and no languages", async () => {
    platform.on("GET", "/api/stores/B2B%20store", status(403, "Forbidden"));
    platform.on("GET", "/api/pagebuilder/designer/storefront", { url: "https://shop.test", previewPath: "/designer-preview" });

    await expect(loadStorefront(STORE)).resolves.toEqual({
      url: "https://shop.test",
      previewPath: "/designer-preview",
      catalog: { storeId: STORE, cultureName: "en-US", currencyCode: "USD" },
      languages: [],
    });
  });

  it("fails when the endpoint refuses", async () => {
    platform.on("GET", "/api/pagebuilder/designer/storefront", status(403, "Forbidden"));

    await expect(loadStorefront(STORE)).rejects.toMatchObject({ status: 403 });
  });
});
