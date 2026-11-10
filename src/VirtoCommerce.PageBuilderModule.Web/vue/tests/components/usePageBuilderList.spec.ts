import { describe, expect, it, vi } from "vitest";
import { defineComponent } from "vue";
import { flushPromises, mount } from "@vue/test-utils";
import { usePageBuilderList, type IUsePageBuilderList } from "../../src/modules/page-builder/composables/usePageBuilderList";
import useStoreParams from "../../src/modules/page-builder/composables/useStoreParams";
import { fakePlatform, type Request } from "./support/fakePlatform";

vi.mock("@vc-shell/framework", async (importOriginal) => ({
  ...(await importOriginal<typeof import("./support/vcShell")>()),
  useDataTablePagination: () => ({ skip: 0, pageSize: 20, currentPage: 1, pages: 1, setPage: vi.fn(), reset: vi.fn(), goToPage: vi.fn() }),
}));

function list(): IUsePageBuilderList {
  let result!: IUsePageBuilderList;
  mount(
    defineComponent({
      setup() {
        result = usePageBuilderList();
        return () => null;
      },
    }),
  );
  return result;
}

describe("the page list", () => {
  it("shows the pages of the last filter picked when an earlier filter's answer arrives after it", async () => {
    const answers = new Map<string, (value: unknown) => void>();
    fakePlatform()
      .on("POST", "/api/page-builder-pages/list", (request: Request) => new Promise((resolve) => answers.set((request.body as { lifecycle: string }).lifecycle, resolve)))
      .install();
    useStoreParams().setStoreId("B2B-store", { url: "", languages: ["en-US"] });
    const pages = list();

    const drafts = pages.loadPages({ lifecycle: "drafts" });
    await flushPromises();
    const archived = pages.loadPages({ lifecycle: "archived" });
    await flushPromises();
    answers.get("archived")!({ totalCount: 1, results: [{ id: "archived", name: "Archived" }] });
    await archived;
    answers.get("drafts")!({ totalCount: 1, results: [{ id: "draft", name: "Draft" }] });
    await drafts;

    expect(pages.items.value.map((x) => x.id)).toEqual(["archived"]);
  });
});
