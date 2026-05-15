import { ref, watch } from "vue";
import type { AssetEntry } from "../useAssetsLibraryApi";
import { getPreviewUrl } from "./assetLibraryHelpers";
import { getAssetKey, isImageEntry } from "./assetLibraryEntry";

export function useAssetSelection() {
  const selectedAsset = ref<AssetEntry>();
  const selectedAssetDimensions = ref<string>();

  watch(selectedAsset, (value) => {
    selectedAssetDimensions.value = undefined;

    if (!value || !isImageEntry(value)) {
      return;
    }

    const previewUrl = getPreviewUrl(value);

    if (!previewUrl) {
      return;
    }

    const image = new Image();
    image.onload = () => {
      if (getPreviewUrl(selectedAsset.value) !== previewUrl) {
        return;
      }

      selectedAssetDimensions.value = `${image.naturalWidth} x ${image.naturalHeight}`;
    };
    image.src = previewUrl;
  });

  function clearSelection() {
    selectedAsset.value = undefined;
  }

  function selectAsset(entry: AssetEntry) {
    selectedAsset.value = entry;
  }

  function refreshSelection(entries: AssetEntry[], preferredSelectionUrl?: string) {
    if (preferredSelectionUrl) {
      selectedAsset.value = entries.find((entry) => getAssetKey(entry) === preferredSelectionUrl);
      return;
    }

    if (!selectedAsset.value) {
      return;
    }

    const selectedKey = getAssetKey(selectedAsset.value);
    selectedAsset.value = selectedKey
      ? entries.find((entry) => getAssetKey(entry) === selectedKey)
      : undefined;
  }

  return {
    selectedAsset,
    selectedAssetDimensions,
    clearSelection,
    selectAsset,
    refreshSelection,
  };
}
