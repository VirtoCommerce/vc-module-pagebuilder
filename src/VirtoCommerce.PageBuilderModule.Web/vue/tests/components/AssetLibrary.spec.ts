import { beforeEach, describe, expect, it, vi } from "vitest";
import { computed, ref } from "vue";
import { mount } from "@vue/test-utils";
import AssetLibraryGrid from "../../src/modules/asset-library/components/AssetLibraryGrid.vue";
import { useAssetLibraryActions } from "../../src/modules/asset-library/composables/useAssetLibraryActions";
import { useAssetReferences } from "../../src/modules/asset-library/composables/useAssetReferences";
import type { AssetEntry, AssetLibraryEntryViewModel } from "../../src/modules/asset-library/types";

const api = vi.hoisted(() => ({ searchAssetReferences: vi.fn() }));
vi.mock("../../src/modules/asset-library/composables/useAssetsLibraryApi", () => ({
  useAssetsLibraryApi: () => ({ searchAssetReferences: api.searchAssetReferences, searchFolderReferences: vi.fn() }),
}));

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
