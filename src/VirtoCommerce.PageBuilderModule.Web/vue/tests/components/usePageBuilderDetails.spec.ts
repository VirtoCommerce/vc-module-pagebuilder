import { beforeEach, describe, expect, it, vi } from "vitest";
import { defineComponent } from "vue";
import { mount } from "@vue/test-utils";
import { usePageBuilderDetails, type IUsePageBuilderDetails } from "../../src/modules/page-builder/composables/usePageBuilderDetails";
import { fakePlatform, status, type FakePlatform } from "./support/fakePlatform";
import { apiClient } from "./support/vcShell";

const CONTENT = "/api/page-builder-pages/grouped/g1/content";
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
    platform = fakePlatform().install();
    Object.assign(apiClient, {
      getGroup: vi.fn(async () => ({ id: "g1", name: "Page", storeId: "s1" })),
      publishStatus: vi.fn(async () => ({ hasChanges: true })),
      updateGroup: vi.fn(async (group: unknown) => group),
    });
  });

  it("fails to load when the page content cannot be read, instead of showing it empty", async () => {
    platform.on("GET", CONTENT, () => status(500));
    const page = details();

    await expect(page.loadGroup()).rejects.toThrow();

    expect(page.seo.value).toEqual({});
    expect(page.item.value.id).toBeUndefined();
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
