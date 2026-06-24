<template>
  <div
    class="assets-library tw-flex tw-h-full tw-flex-col"
  >
    <AssetLibraryToolbar
      :can-create="canCreate"
      :search-value="searchValue"
      :total-count="totalCount"
      v-model:view-mode="viewMode"
      @upload="openUploadDialog"
      @create-folder="openCreateFolderPopup"
      @search-change="onSearchChange"
    />

    <div class="assets-library__body">
      <section
        class="assets-library__content"
        :class="{ 'assets-library__content--drag-over': isDraggingOverSurface }"
        @click="clearSelection"
        @dragenter.prevent="handleSurfaceDrag"
        @dragover.prevent="handleSurfaceDrag"
        @dragleave="handleSurfaceDragLeave"
        @drop.prevent="handleSurfaceDrop"
      >
        <div
          class="assets-library__breadcrumbs"
          @click.stop
        >
          <VcBreadcrumbs
            :items="breadcrumbs"
            separated
          />
        </div>

        <div
          v-if="isDraggingOverSurface"
          class="assets-library__drop-hint"
        >
          <VcIcon
            icon="lucide-cloud-upload"
            class="assets-library__drop-hint-icon"
          />
          <span>{{ $t("ASSET_LIBRARY.DROP.UPLOAD_HERE") }}</span>
        </div>

        <AssetLibraryGrid
          v-if="viewMode === 'grid' && (loading || entryViewModels.length)"
          :entries="entryViewModels"
          :loading="loading"
          :can-delete="canDelete"
          @entry-click="handleEntryClick"
          @folder-drag="handleFolderDrag"
          @folder-drag-leave="handleFolderDragLeave"
          @folder-drop="handleFolderDrop"
          @copy="copyAssetUrl"
          @delete="confirmDelete"
        />

        <AssetLibraryTable
          v-else-if="viewMode === 'table' && (loading || entryViewModels.length)"
          :entries="entryViewModels"
          :loading="loading"
          :selected-entry-key="selectedEntryKey"
          :can-delete="canDelete"
          @entry-click="handleEntryClick"
          @copy="copyAssetUrl"
          @delete="confirmDelete"
        />

        <div
          v-else-if="!loading"
          class="assets-library__empty"
        >
          <VcIcon
            icon="lucide-folder-open"
            class="assets-library__empty-icon"
          />
          <div class="assets-library__empty-title">
            {{ storeId ? $t("ASSET_LIBRARY.EMPTY.TITLE") : $t("ASSET_LIBRARY.EMPTY.NO_STORE") }}
          </div>
          <VcHint class="assets-library__empty-text">
            {{ $t("ASSET_LIBRARY.EMPTY.DESCRIPTION") }}
          </VcHint>
          <VcFileUpload
            v-if="canCreate && storeId"
            class="assets-library__empty-upload"
            variant="file-upload"
            accept=""
            multiple
            name="assets-library-upload"
            :loading="loading"
            :custom-text="{
              dragHere: $t('ASSET_LIBRARY.DROP.UPLOAD_HERE'),
              browse: $t('ASSET_LIBRARY.TOOLBAR.UPLOAD'),
            }"
            @upload="uploadAssets"
          />
        </div>
      </section>

      <AssetLibraryDetails
        v-if="selectedAssetView"
        :selected-asset="selectedAssetView"
        :can-create="canCreate"
        :can-delete="canDelete"
        @close="clearSelection"
        @replace="openReplaceDialog"
        @copy="copyAssetUrl"
        @delete="confirmDelete"
      />
    </div>
  </div>

  <input
    ref="replaceInputRef"
    type="file"
    hidden
    @change="onReplaceChange"
  />

  <VcPopup
    v-if="isUploadPopupOpen"
    v-model="isUploadPopupOpen"
    :title="$t('ASSET_LIBRARY.TOOLBAR.UPLOAD')"
    is-mobile-fullscreen
    @close="closeUploadPopup"
  >
    <template #content>
      <div class="assets-library__upload-popup">
        <VcFileUpload
          variant="file-upload"
          accept=""
          multiple
          name="assets-library-toolbar-upload"
          :loading="loading"
          :custom-text="{
            dragHere: $t('ASSET_LIBRARY.DROP.UPLOAD_HERE'),
            browse: $t('ASSET_LIBRARY.TOOLBAR.UPLOAD'),
          }"
          @upload="handlePopupUpload"
        />
      </div>
    </template>
  </VcPopup>

  <CreateFolderPopup
    v-if="isCreateFolderPopupOpen"
    :submitting="loading"
    :server-error="createFolderError"
    @clear-error="clearCreateFolderError"
    @close="closeCreateFolderPopup"
    @create="handleCreateFolder"
  />
</template>

<script lang="ts" setup>
import { computed, onMounted, ref } from "vue";
import { debounce } from "lodash-es";
import { useI18n } from "vue-i18n";
import { usePermissions } from "@vc-shell/framework";
import { VcBreadcrumbs, VcFileUpload, VcHint, VcIcon, VcPopup } from "@vc-shell/framework/ui";
import type { AssetEntry } from "../types";
import { useAssetsLibrary } from "../composables/useAssetsLibrary";
import { useAssetLibraryActions } from "../composables/useAssetLibraryActions";
import { useAssetLibraryDragDrop } from "../composables/useAssetLibraryDragDrop";
import AssetLibraryDetails from "./AssetLibraryDetails.vue";
import AssetLibraryGrid from "./AssetLibraryGrid.vue";
import AssetLibraryTable from "./AssetLibraryTable.vue";
import AssetLibraryToolbar from "./AssetLibraryToolbar.vue";
import CreateFolderPopup from "./CreateFolderPopup.vue";
import type { AssetLibraryViewMode } from "../types";
import {
  createAssetLibraryDetailsViewModel,
  createAssetLibraryEntryViewModel,
  getAssetEntryKey,
} from "../utilities/viewModels";

export interface ExposedAssetsLibraryContent {
  reload: () => Promise<void>;
}

const { t } = useI18n({ useScope: "global" });
const { hasAccess } = usePermissions();

const replaceInputRef = ref<HTMLInputElement | null>(null);
const createFolderError = ref<string>();
const isCreateFolderPopupOpen = ref(false);
const isUploadPopupOpen = ref(false);
const viewMode = ref<AssetLibraryViewMode>("grid");

const {
  entries,
  loading,
  totalCount,
  searchValue,
  selectedAsset,
  selectedAssetDimensions,
  storeId,
  breadcrumbs,
  initialize,
  reload,
  clearSelection,
  onEntryClick,
  isImage,
  getEntryIcon,
  getReferencesCount,
  getReferencePages,
  getDeleteReferencesCount,
  formatFileSize,
  formatDate,
  getAssetPublicUrl,
  getPreviewUrl,
  createFolder,
  uploadFiles,
  replaceSelectedAsset,
  deleteEntry,
} = useAssetsLibrary();

const canCreate = computed(() => hasAccess("platform:asset:create"));
const canDelete = computed(() => hasAccess("platform:asset:delete"));
const {
  notifyError,
  uploadAssets,
  createAssetFolder,
  replaceAsset,
  copyAssetUrl,
  confirmDelete,
} = useAssetLibraryActions({
  t,
  canCreate,
  uploadFiles,
  createFolder,
  replaceSelectedAsset,
  deleteEntry,
  getDeleteReferencesCount,
  getAssetPublicUrl,
});
const {
  isDraggingOverSurface,
  draggedFolderUrl,
  getEntryDropFolderUrl,
  handleSurfaceDrag,
  handleSurfaceDragLeave,
  handleSurfaceDrop,
  handleFolderDrag,
  handleFolderDragLeave,
  handleFolderDrop,
} = useAssetLibraryDragDrop(canCreate, uploadAssets);
const selectedEntryKey = computed(() => getAssetEntryKey(selectedAsset.value));
const notAvailableText = computed(() => t("ASSET_LIBRARY.DETAILS.NOT_AVAILABLE"));
const entryViewModels = computed(() => entries.value.map(entry => createAssetLibraryEntryViewModel(entry, {
  selectedEntryKey: selectedEntryKey.value,
  draggedFolderUrl: draggedFolderUrl.value,
  notAvailableText: notAvailableText.value,
  getEntryDropFolderUrl,
  isImage,
  getEntryIcon,
  getReferencesCount,
  formatFileSize,
  formatDate,
  getPreviewUrl,
})));
const selectedAssetView = computed(() => {
  const asset = selectedAsset.value;
  return asset ? createAssetLibraryDetailsViewModel(asset, {
    selectedEntryKey: selectedEntryKey.value,
    draggedFolderUrl: draggedFolderUrl.value,
    notAvailableText: notAvailableText.value,
    getEntryDropFolderUrl,
    isImage,
    getEntryIcon,
    getReferencesCount,
    formatFileSize,
    formatDate,
    getPreviewUrl,
    dimensions: selectedAssetDimensions.value,
    getReferencePages,
  }) : undefined;
});

const onSearchChange = debounce(async (keyword: string | undefined) => {
  searchValue.value = keyword;

  try {
    await reload();
  } catch (error) {
    notifyError(error);
  }
}, 350);

function openUploadDialog() {
  isUploadPopupOpen.value = true;
}

function closeUploadPopup() {
  isUploadPopupOpen.value = false;
}

function openReplaceDialog() {
  if (replaceInputRef.value) {
    replaceInputRef.value.value = "";
  }

  replaceInputRef.value?.click();
}

function openCreateFolderPopup() {
  clearCreateFolderError();
  isCreateFolderPopupOpen.value = true;
}

function closeCreateFolderPopup() {
  clearCreateFolderError();
  isCreateFolderPopupOpen.value = false;
}

function clearCreateFolderError() {
  createFolderError.value = undefined;
}

async function handleEntryClick(entry: AssetEntry) {
  try {
    await onEntryClick(entry);
  } catch (error) {
    notifyError(error);
  }
}

async function handleCreateFolder(name: string) {
  clearCreateFolderError();

  const result = await createAssetFolder(name);

  if (result.succeeded) {
    closeCreateFolderPopup();
    return;
  }

  createFolderError.value = result.errorMessage;
}

async function onReplaceChange(event: Event) {
  const input = event.target as HTMLInputElement;
  const replacement = input.files?.[0];

  if (!replacement) {
    input.value = "";
    return;
  }

  await replaceAsset(replacement);
  input.value = "";
}

async function handlePopupUpload(files: FileList) {
  const uploaded = await uploadAssets(files);

  if (uploaded) {
    closeUploadPopup();
  }
}

async function reloadContent() {
  try {
    await reload();
  } catch (error) {
    notifyError(error);
  }
}

onMounted(async () => {
  try {
    await initialize();
  } catch (error) {
    notifyError(error);
  }
});

defineExpose<ExposedAssetsLibraryContent>({
  reload: reloadContent,
});
</script>

<style lang="scss" scoped>
.assets-library {
  --assets-library-bg: var(--neutrals-50);
  --assets-library-panel: var(--additional-50);
  --assets-library-border: var(--neutrals-200);
  --assets-library-text: var(--neutrals-800);
  --assets-library-text-muted: var(--neutrals-500);
  --assets-library-selected: var(--primary-500);
  --assets-library-reference-bg: color-mix(in srgb, var(--assets-library-selected), transparent 84%);
  --assets-library-reference-text: var(--primary-700);
  --assets-library-preview: linear-gradient(
    135deg,
    color-mix(in srgb, var(--assets-library-panel), var(--assets-library-selected) 5%) 0%,
    color-mix(in srgb, var(--assets-library-bg), var(--assets-library-selected) 8%) 100%
  );
  --assets-library-folder: linear-gradient(
    135deg,
    color-mix(in srgb, var(--assets-library-selected), var(--assets-library-panel) 34%) 0%,
    color-mix(in srgb, var(--assets-library-selected), var(--assets-library-bg) 20%) 100%
  );
  --assets-library-details: var(--additional-50);
  --assets-library-drop: color-mix(in srgb, var(--assets-library-selected), transparent 88%);
  --assets-library-checker-bg: color-mix(in srgb, var(--assets-library-panel), var(--assets-library-bg) 50%);
  --assets-library-checker-tile: color-mix(in srgb, var(--assets-library-border), transparent 25%);
  @apply tw-bg-[color:var(--assets-library-bg)] tw-text-sm tw-leading-[18px] tw-text-[color:var(--assets-library-text)];

  button,
  input,
  select,
  textarea {
    font: inherit;
  }

  &__body {
    @apply tw-flex tw-min-h-0 tw-flex-1 tw-flex-col lg:tw-flex-row;
  }

  &__content {
    @apply tw-relative tw-flex tw-min-h-0 tw-grow tw-basis-0 tw-flex-col;
    transition: background-color 0.16s ease, box-shadow 0.16s ease;
  }

  &__content--drag-over {
    background-color: var(--assets-library-drop);
    box-shadow: inset 0 0 0 2px color-mix(in srgb, var(--primary-500), transparent 65%);
  }

  &__drop-hint {
    @apply tw-pointer-events-none tw-absolute tw-inset-x-4 tw-top-20 tw-z-20 tw-flex tw-items-center tw-justify-center tw-gap-2 tw-rounded-lg tw-border tw-border-dashed tw-border-[color:var(--assets-library-selected)] tw-p-4 tw-text-sm tw-font-semibold tw-text-[color:var(--assets-library-selected)] tw-shadow-sm;
    background-color: color-mix(in srgb, var(--assets-library-panel), transparent 8%);
  }

  &__drop-hint-icon {
    @apply tw-text-xl;
  }

  &__breadcrumbs {
    @apply tw-border-b tw-border-solid tw-border-b-[color:var(--assets-library-border)] tw-bg-[color:var(--assets-library-panel)] tw-px-4 tw-py-2;
  }

  &__empty {
    @apply tw-flex tw-h-full tw-flex-1 tw-flex-col tw-items-center tw-justify-center tw-gap-3 tw-p-8;
  }

  &__empty-icon {
    @apply tw-text-[64px] tw-text-[color:var(--assets-library-selected)];
  }

  &__empty-title {
    @apply tw-text-lg tw-font-semibold;
  }

  &__empty-text {
    @apply tw-max-w-[420px] tw-text-center;
  }

  &__empty-upload {
    @apply tw-mt-3 tw-w-full tw-max-w-[420px];
  }

  &__upload-popup {
    @apply tw-p-4;
  }
}
</style>
