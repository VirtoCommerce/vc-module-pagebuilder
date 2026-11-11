import { beforeEach, describe, expect, it } from "vitest";
import { defineComponent } from "vue";
import { mount } from "@vue/test-utils";
import { usePageBuilderDetails, type IUsePageBuilderDetails } from "../../src/modules/page-builder/composables/usePageBuilderDetails";
import { fakePlatform, status, type FakePlatform, type Request } from "./support/fakePlatform";

const GROUP = "/api/page-builder-pages/grouped/g1";
const CONTENT = `${GROUP}/content`;
const PUBLISH_STATUS = "/api/page-builder-pages/grouped/publish-status/g1";
const SEO = { pageTitle: "Title", metaDescription: "Description", metaKeywords: "Keywords" };
const stored = { content: JSON.stringify({ settings: { type: "settings", seoInfo: SEO }, content: [] }), eTag: '"e1"' };

function details(): IUsePageBuilderDetails {
  let result!: IUsePageBuilderDetails;
  mount(
    defineComponent({
      setup() {
        result = usePageBuilderDetails({ id: "g1" });
        return () => null;
      },
    }),
  );
  return result;
}

describe("the SEO of a page's details", () => {
  let platform: FakePlatform;

  beforeEach(() => {
    platform = fakePlatform()
      .on("GET", GROUP, { id: "g1", name: "Page", storeId: "s1" })
      .on("GET", PUBLISH_STATUS, { hasChanges: true })
      .on("PUT", "/api/page-builder-pages/grouped", (request: Request) => request.body)
      .install();
  });

  it("fails to load when the page content cannot be read, instead of showing it empty", async () => {
    platform.on("GET", CONTENT, () => status(500));
    const page = details();

    await expect(page.loadGroup()).rejects.toThrow();

    expect(page.seo.value).toEqual({});
    expect(page.item.value.id).toBeUndefined();
  });

  it("refuses to archive a page whose details failed to load, instead of reporting it archived", async () => {
    platform.on("GET", CONTENT, () => status(500));
    const page = details();
    await page.loadGroup().catch(() => undefined);

    await expect(page.deleteGroup()).rejects.toThrow();

    expect(platform.to("/api/page-builder-pages/grouped/archive")).toEqual([]);
  });

  it("keeps the stored SEO when a later read fails, so the next save does not blank it", async () => {
    platform.on("GET", CONTENT, stored);
    const page = details();
    await page.loadGroup();

    platform.on("GET", CONTENT, () => status(500));
    await page.loadGroup().catch(() => undefined);
    platform.on("GET", CONTENT, stored).on("POST", CONTENT, { eTag: '"e2"' });
    page.seo.value.pageTitle = "New title";
    await page.saveGroup();

    const [save] = platform.to(CONTENT, "POST");
    expect((save.body as { settings: { seoInfo: unknown } }).settings.seoInfo).toEqual({ ...SEO, pageTitle: "New title" });
  });
});

describe("saving a page's details", () => {
  let publishStatus: { hasChanges: boolean };

  beforeEach(() => {
    publishStatus = { hasChanges: false };
    fakePlatform()
      .install()
      .on("GET", CONTENT, stored)
      .on("GET", GROUP, { id: "g1", name: "Page", storeId: "s1" })
      .on("GET", PUBLISH_STATUS, () => publishStatus)
      .on("PUT", "/api/page-builder-pages/grouped", (request: Request) => request.body);
  });

  it("reads the publish status again when only the name changed, since the server keeps the new name in a draft", async () => {
    const page = details();
    await page.loadGroup();
    publishStatus = { hasChanges: true };

    page.item.value.name = "Renamed";
    await page.saveGroup();

    expect(page.status.value).toEqual({ hasChanges: true });
  });
});
