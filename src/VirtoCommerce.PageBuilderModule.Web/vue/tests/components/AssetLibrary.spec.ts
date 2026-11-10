import { beforeEach, describe, expect, it, vi } from "vitest";
import { computed, defineComponent, ref } from "vue";
import { flushPromises, mount, type VueWrapper } from "@vue/test-utils";
import AssetLibraryGrid from "../../src/modules/asset-library/components/AssetLibraryGrid.vue";
import AssetsLibraryContent from "../../src/modules/asset-library/components/AssetsLibraryContent.vue";
import { useAssetLibraryActions, type AssetUploadConflictDecision } from "../../src/modules/asset-library/composables/useAssetLibraryActions";
import { useAssetReferences } from "../../src/modules/asset-library/composables/useAssetReferences";
import { useAssetsLibrary } from "../../src/modules/asset-library/composables/useAssetsLibrary";
import type { AssetEntry, AssetLibraryEntryViewModel } from "../../src/modules/asset-library/types";
import { createAssetReferenceDetails } from "../../src/modules/asset-library/utilities/assetReferences";
import { notification } from "./support/vcShell";

const api = vi.hoisted(() => ({
  searchAssetReferences: vi.fn(),
  searchAssets: vi.fn(),
  uploadAsset: vi.fn(),
  createAssetFolder: vi.fn(),
  deleteAssets: vi.fn(),
}));
vi.mock("../../src/modules/asset-library/composables/useAssetsLibraryApi", () => ({
  useAssetsLibraryApi: () => ({ ...api, searchFolderReferences: vi.fn() }),
}));

vi.mock("@vc-shell/framework", async (importOriginal) => {
  const shell = await importOriginal<typeof import("./support/vcShell")>();
  const { computed: vueComputed } = await import("vue");
  return {
    ...shell,
    isImage: (name: string) => /\.(png|jpe?g|gif|webp|svg)$/i.test(name),
    readableSize: (size: number) => `${size} B`,
    useBreadcrumbs: () => ({ breadcrumbs: vueComputed(() => []), push: vi.fn(), remove: vi.fn() }),
    useDataTablePagination: () => ({ skip: 0, pageSize: 20, currentPage: 1, pages: 1, setPage: vi.fn(), reset: vi.fn(), goToPage: vi.fn() }),
  };
});

vi.mock("@vc-shell/framework/ui", async (importOriginal) => {
  const ui = await importOriginal<typeof import("./support/vcShellUi")>();
  const { defineComponent: define, h: render } = await import("vue");
  const box = (name: string, props: string[]) => define({ name, props, setup: (_, { slots }) => () => render("div", slots.default?.()) });
  return {
    ...ui,
    VcBreadcrumbs: box("VcBreadcrumbs", ["items", "separated"]),
    VcPagination: box("VcPagination", ["currentPage", "pages"]),
    VcFileUpload: box("VcFileUpload", ["accept", "variant", "multiple", "name", "loading", "customText"]),
  };
});

vi.mock("../../src/modules/page-builder/composables", async () => {
  const { ref: vueRef } = await import("vue");
  return {
    useUrlParams: () => ({ storeId: vueRef("s1"), storeContextStatus: vueRef("ready"), initUrlParams: vi.fn(), validateStoreContext: async () => true }),
  };
});

const FOLDER: AssetEntry = { type: "folder", name: "images", relativeUrl: "/images" };
const BLOB: AssetEntry = { type: "blob", name: "a.txt", relativeUrl: "/a.txt", url: "https://cdn.test/a.txt" };

describe("dragging over the asset grid", () => {
  const viewModel = (entry: AssetEntry, dropFolderUrl?: string) =>
    ({ key: entry.name, entry, isFolder: entry.type === "folder", isBlob: entry.type === "blob", dropFolderUrl }) as AssetLibraryEntryViewModel;

  function grid() {
    const wrapper = mount(AssetLibraryGrid, { props: { entries: [viewModel(FOLDER, "/images"), viewModel(BLOB)], canDelete: false } });
    const reachedSurface = vi.fn();
    for (const type of ["dragenter", "dragover", "dragleave", "drop"]) wrapper.element.addEventListener(type, reachedSurface);
    const [folder, file] = wrapper.findAll("article");
    return { wrapper, folder, file, reachedSurface };
  }

  it.each(["dragenter", "dragover", "dragleave", "drop"])("%s on a file card reaches the surface and is not a folder's", async (type) => {
    const { wrapper, file, reachedSurface } = grid();

    await file.trigger(type);

    expect(reachedSurface).toHaveBeenCalledTimes(1);
    expect(wrapper.emitted("folder-drag")).toBeUndefined();
    expect(wrapper.emitted("folder-drag-leave")).toBeUndefined();
    expect(wrapper.emitted("folder-drop")).toBeUndefined();
  });

  it.each([
    ["dragenter", "folder-drag"],
    ["dragover", "folder-drag"],
    ["dragleave", "folder-drag-leave"],
    ["drop", "folder-drop"],
  ])("%s on a folder card stays with the folder", async (type, event) => {
    const { wrapper, folder, reachedSurface } = grid();

    await folder.trigger(type);

    expect(reachedSurface).not.toHaveBeenCalled();
    expect(wrapper.emitted(event)?.[0][0]).toEqual(FOLDER);
  });

  it.each(["dragenter", "dragover", "drop"])("%s on a file card still allows the drop", async (type) => {
    const { file } = grid();
    const event = new Event(type, { bubbles: true, cancelable: true });

    file.element.dispatchEvent(event);

    expect(event.defaultPrevented).toBe(true);
  });
});

describe("uploading assets", () => {
  it("uploads into the folder that was open when the upload started", async () => {
    const currentFolderUrl = ref("/first");
    const uploadFiles = vi.fn(async () => undefined);
    const { uploadAssets } = useAssetLibraryActions({
      t: (key) => key,
      canCreate: computed(() => true),
      currentFolderUrl,
      uploadFiles,
      createFolder: vi.fn(),
      replaceSelectedAsset: vi.fn(),
      deleteEntry: vi.fn(),
      getDeleteReferences: vi.fn(),
      findAssetByName: async () => {
        currentFolderUrl.value = "/second";
        return undefined;
      },
      requestUploadConflictDecision: vi.fn(),
      getAssetPublicUrl: () => undefined,
    });

    await uploadAssets([new File(["x"], "a.txt")]);

    expect(uploadFiles).toHaveBeenCalledWith([expect.any(File)], "/first");
  });

  it("asks about a second batch's name conflict only once the first batch is decided, and uploads both", async () => {
    // One pending decision at a time, as the library's overwrite dialog holds it.
    let decide: ((decision: AssetUploadConflictDecision) => void) | undefined;
    const requestUploadConflictDecision = vi.fn(() => new Promise<AssetUploadConflictDecision>((resolve) => (decide = resolve)));
    const uploadFiles = vi.fn(async (_files: FileList | File[], _folderUrl?: string) => undefined);
    const { uploadAssets } = useAssetLibraryActions({
      t: (key) => key,
      canCreate: computed(() => true),
      currentFolderUrl: ref("/folder"),
      uploadFiles,
      createFolder: vi.fn(),
      replaceSelectedAsset: vi.fn(),
      deleteEntry: vi.fn(),
      getDeleteReferences: async () => ({ ...createAssetReferenceDetails([]), usageKnown: true }),
      findAssetByName: async (folderUrl, name) => ({ type: "blob", name, relativeUrl: `${folderUrl}/${name}` }),
      requestUploadConflictDecision,
      getAssetPublicUrl: () => undefined,
    });

    const first = uploadAssets([new File(["1"], "a.png")]);
    const second = uploadAssets([new File(["2"], "b.png")]);
    await flushPromises();

    expect(requestUploadConflictDecision).toHaveBeenCalledTimes(1);

    decide!({ action: "replace" });
    await flushPromises();

    expect(requestUploadConflictDecision).toHaveBeenCalledTimes(2);

    decide!({ action: "replace" });

    await expect(Promise.all([first, second])).resolves.toEqual([true, true]);
    expect(uploadFiles.mock.calls.map(([files]) => Array.from(files, (x) => x.name))).toEqual([["a.png"], ["b.png"]]);
  });

  describe("of accepted types only", () => {
    const PNG = new File(["1"], "a.png", { type: "image/png" });
    const PDF = new File(["2"], "doc.pdf", { type: "application/pdf" });

    function actions(acceptedTypes?: string[]) {
      const uploadFiles = vi.fn(async (_files: FileList | File[], _folderUrl?: string) => undefined);
      const { uploadAssets } = useAssetLibraryActions({
        t: (key, params) => (params ? `${key} ${JSON.stringify(params)}` : key),
        canCreate: computed(() => true),
        currentFolderUrl: ref("/folder"),
        uploadFiles,
        createFolder: vi.fn(),
        replaceSelectedAsset: vi.fn(),
        deleteEntry: vi.fn(),
        getDeleteReferences: vi.fn(),
        findAssetByName: async () => undefined,
        requestUploadConflictDecision: vi.fn(),
        getAssetPublicUrl: () => undefined,
        acceptedTypes,
      });
      const uploaded = () => uploadFiles.mock.calls.map(([files]) => Array.from(files, (x) => x.name));
      return { uploadAssets, uploaded };
    }

    it("does not upload a file of another type, says which, and uploads the rest of the batch", async () => {
      const { uploadAssets, uploaded } = actions(["image/*"]);

      await expect(uploadAssets([PNG, PDF])).resolves.toBe(true);

      expect(uploaded()).toEqual([["a.png"]]);
      expect(notification.warning).toHaveBeenCalledTimes(1);
      expect(notification.warning).toHaveBeenCalledWith('ASSET_LIBRARY.NOTIFICATIONS.NOT_ACCEPTED {"names":"doc.pdf","types":"image/*"}');
    });

    it("uploads nothing when no file is of an accepted type", async () => {
      const { uploadAssets, uploaded } = actions(["image/*"]);

      await expect(uploadAssets([PDF])).resolves.toBe(false);

      expect(uploaded()).toEqual([]);
      expect(notification.warning).toHaveBeenCalledTimes(1);
    });

    it("uploads every file when no types are given", async () => {
      const { uploadAssets, uploaded } = actions();

      await uploadAssets([PNG, PDF]);

      expect(uploaded()).toEqual([["a.png", "doc.pdf"]]);
      expect(notification.warning).not.toHaveBeenCalled();
    });
  });
});

describe("loading an asset's reference details", () => {
  beforeEach(() => {
    api.searchAssetReferences.mockReset();
  });

  it("marks the asset's references unavailable when the search fails", async () => {
    api.searchAssetReferences.mockRejectedValue(new Error("boom"));
    const references = useAssetReferences(ref("store"));

    await expect(references.loadAssetReferenceDetails(BLOB)).resolves.toBe(true);

    expect(references.areReferencesAvailable(BLOB)).toBe(false);
  });

  it("leaves the state alone when a failed search is no longer current", async () => {
    api.searchAssetReferences.mockRejectedValue(new Error("boom"));
    const references = useAssetReferences(ref("store"));

    await expect(references.loadAssetReferenceDetails(BLOB, () => false)).resolves.toBe(false);

    expect(references.areReferencesAvailable(BLOB)).toBe(true);
  });

  it("makes the references available again once a later search succeeds", async () => {
    api.searchAssetReferences.mockRejectedValueOnce(new Error("boom")).mockResolvedValueOnce([]);
    const references = useAssetReferences(ref("store"));

    await references.loadAssetReferenceDetails(BLOB);
    await references.loadAssetReferenceDetails(BLOB);

    expect(references.areReferencesAvailable(BLOB)).toBe(true);
  });
});

describe("a failed asset library action", () => {
  const ROOT = "/stores/s1/Page Builder";
  const refused = new Error("Refused by the platform");

  function assetLibrary() {
    let library!: ReturnType<typeof useAssetsLibrary>;
    let actions!: ReturnType<typeof useAssetLibraryActions>;
    mount(
      defineComponent({
        setup() {
          library = useAssetsLibrary();
          actions = useAssetLibraryActions({
            t: (key) => key,
            canCreate: computed(() => true),
            currentFolderUrl: library.currentFolderUrl,
            uploadFiles: library.uploadFiles,
            createFolder: library.createFolder,
            replaceSelectedAsset: library.replaceSelectedAsset,
            deleteEntry: library.deleteEntry,
            getDeleteReferences: library.getDeleteReferences,
            findAssetByName: library.findAssetByName,
            requestUploadConflictDecision: vi.fn(),
            getAssetPublicUrl: () => undefined,
          });
          return () => null;
        },
      }),
    );
    library.currentFolderUrl.value = ROOT;
    return { library, actions };
  }

  beforeEach(() => {
    for (const fn of Object.values(api)) fn.mockReset();
    api.searchAssets.mockResolvedValue({ results: [], totalCount: 0 });
    api.searchAssetReferences.mockResolvedValue([]);
  });

  it("shows one error toast when an upload fails", async () => {
    api.uploadAsset.mockRejectedValue(refused);
    const { actions } = assetLibrary();

    await actions.uploadAssets([new File(["x"], "b.txt")]);

    expect(notification.error).toHaveBeenCalledTimes(1);
    expect(notification.error).toHaveBeenCalledWith(refused.message);
  });

  it("shows one error toast when a replace fails", async () => {
    api.uploadAsset.mockRejectedValue(refused);
    const { library, actions } = assetLibrary();
    library.selectedAsset.value = BLOB;

    await actions.replaceAsset(new File(["x"], "other.txt"));

    expect(notification.error).toHaveBeenCalledTimes(1);
  });

  it("shows one error toast when a delete fails", async () => {
    api.deleteAssets.mockRejectedValue(refused);
    const { actions } = assetLibrary();

    await actions.confirmDelete(BLOB);

    expect(notification.error).toHaveBeenCalledTimes(1);
  });

  it("reports a failed new folder only in the folder dialog", async () => {
    api.createAssetFolder.mockRejectedValue(refused);
    const { actions } = assetLibrary();

    const result = await actions.createAssetFolder("photos");

    expect(result).toEqual({ succeeded: false, errorMessage: refused.message });
    expect(notification.error).not.toHaveBeenCalled();
  });
});

describe("the asset picker", () => {
  const ROOT = "/stores/s1/Page Builder";
  const PNG: AssetEntry = { type: "blob", name: "a.png", relativeUrl: `${ROOT}/a.png`, contentType: "image/png" };
  const PDF: AssetEntry = { type: "blob", name: "doc.pdf", relativeUrl: `${ROOT}/doc.pdf`, contentType: "application/pdf" };
  const stubs = ["AssetLibraryToolbar", "AssetLibraryGrid", "AssetLibraryTable", "AssetLibraryDetails", "CreateFolderPopup", "AssetOverwritePopup"];

  beforeEach(() => {
    for (const fn of Object.values(api)) fn.mockReset();
    api.searchAssetReferences.mockResolvedValue([]);
  });

  async function mountLibrary(props: { pickable?: boolean; accept?: string[] }, results: AssetEntry[] = [PNG, PDF]) {
    api.searchAssets.mockResolvedValue({ results, totalCount: results.length });
    const wrapper = mount(AssetsLibraryContent, { props, global: { stubs: Object.fromEntries(stubs.map((name) => [name, true])) } });
    await flushPromises();
    return wrapper;
  }

  const select = (wrapper: VueWrapper) => wrapper.findAll("button").find((x) => x.text() === "Select")!;
  async function choose(wrapper: VueWrapper, entry: AssetEntry) {
    wrapper.findComponent({ name: "AssetLibraryGrid" }).vm.$emit("entry-click", entry);
    await flushPromises();
  }

  it("asks the server for the accepted types only", async () => {
    await mountLibrary({ pickable: true, accept: ["image/*"] });

    expect(api.searchAssets).toHaveBeenCalledWith(ROOT, undefined, expect.objectContaining({ acceptedTypes: ["image/*"] }));
  });

  it("selects an asset of an accepted type and no other", async () => {
    const wrapper = await mountLibrary({ pickable: true, accept: ["image/*"] });

    await choose(wrapper, PDF);
    expect(select(wrapper).attributes("disabled")).toBeDefined();

    await choose(wrapper, PNG);
    await select(wrapper).trigger("click");
    expect(wrapper.emitted("pick")).toEqual([[`/assets${ROOT}/a.png`]]);
  });

  it("offers only the accepted types in its upload dialog", async () => {
    const wrapper = await mountLibrary({ pickable: true, accept: [".svg", "image/png"] }, []);

    expect(wrapper.findComponent({ name: "VcFileUpload" }).props("accept")).toBe(".svg,image/png");
  });

  it("lists, selects and uploads any type when no types are given", async () => {
    const wrapper = await mountLibrary({ pickable: true });

    expect(api.searchAssets.mock.calls[0][2]).not.toHaveProperty("acceptedTypes");
    await choose(wrapper, PDF);
    expect(select(wrapper).attributes("disabled")).toBeUndefined();
  });

  it("leaves the library blade's upload open to any type", async () => {
    const wrapper = await mountLibrary({}, []);

    expect(wrapper.findComponent({ name: "VcFileUpload" }).props("accept")).toBe("");
  });
});
