<template>
  <div
    v-loading="loading"
    class="assets-library tw-flex tw-h-full tw-flex-col"
  >
    <AssetLibraryToolbar
      :can-create="canCreate"
      :search-value="searchValue"
      :total-count="totalCount"
      :view-mode="viewMode"
      @upload="openUploadDialog"
      @create-folder="openCreateFolderPopup"
      @search-change="onSearchChange"
      @update:view-mode="viewMode = $event"
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
            icon="material-cloud_upload"
            class="assets-library__drop-hint-icon"
          />
          <span>{{ $t("PAGE_BUILDER.ASSETS.DROP.UPLOAD_HERE") }}</span>
        </div>

        <AssetLibraryGrid
          v-if="entryViewModels.length && viewMode === 'grid'"
          :entries="entryViewModels"
          :can-delete="canDelete"
          @entry-click="handleEntryClick"
          @folder-drag="handleFolderDrag"
          @folder-drag-leave="handleFolderDragLeave"
          @folder-drop="handleFolderDrop"
          @copy="copyAssetUrl"
          @delete="confirmDelete"
        />

        <AssetLibraryTable
          v-else-if="entryViewModels.length"
          :entries="entryViewModels"
          :loading="loading"
          :selected-entry-key="selectedEntryKey"
          :can-delete="canDelete"
          @entry-click="handleEntryClick"
          @copy="copyAssetUrl"
          @delete="confirmDelete"
        />

        <div
          v-else
          class="assets-library__empty"
        >
          <VcIcon
            icon="material-folder_open"
            class="assets-library__empty-icon"
          />
          <div class="assets-library__empty-title">
            {{ storeId ? $t("PAGE_BUILDER.ASSETS.EMPTY.TITLE") : $t("PAGE_BUILDER.ASSETS.EMPTY.NO_STORE") }}
          </div>
          <VcHint class="assets-library__empty-text">
            {{ $t("PAGE_BUILDER.ASSETS.EMPTY.DESCRIPTION") }}
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
              dragHere: $t('PAGE_BUILDER.ASSETS.DROP.UPLOAD_HERE'),
              browse: $t('PAGE_BUILDER.ASSETS.TOOLBAR.UPLOAD'),
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
    :title="$t('PAGE_BUILDER.ASSETS.TOOLBAR.UPLOAD')"
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
            dragHere: $t('PAGE_BUILDER.ASSETS.DROP.UPLOAD_HERE'),
            browse: $t('PAGE_BUILDER.ASSETS.TOOLBAR.UPLOAD'),
          }"
          @upload="handlePopupUpload"
        />
      </div>
    </template>
  </VcPopup>

  <CreateFolderPopup
    v-if="isCreateFolderPopupOpen"
    :submitting="loading"
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
import type { AssetEntry } from "../composables/useAssetsLibraryApi";
import { useAssetsLibrary } from "../composables/useAssetsLibrary";
import { useAssetLibraryActions } from "../composables/useAssetsLibrary/useAssetLibraryActions";
import { useAssetLibraryDragDrop } from "../composables/useAssetsLibrary/useAssetLibraryDragDrop";
import AssetLibraryDetails from "./AssetLibraryDetails.vue";
import AssetLibraryGrid from "./AssetLibraryGrid.vue";
import AssetLibraryTable from "./AssetLibraryTable.vue";
import AssetLibraryToolbar from "./AssetLibraryToolbar.vue";
import CreateFolderPopup from "./CreateFolderPopup.vue";
import type { AssetLibraryViewMode } from "./assetLibraryTypes";
import {
  createAssetLibraryDetailsViewModel,
  createAssetLibraryEntryViewModel,
  getAssetEntryKey,
} from "./assetLibraryViewModels";

export interface ExposedAssetsLibraryContent {
  reload: () => Promise<void>;
}

const { t } = useI18n({ useScope: "global" });
const { hasAccess } = usePermissions();

const replaceInputRef = ref<HTMLInputElement | null>(null);
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
} = useAssetsLibrary(t);

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
const notAvailableText = computed(() => t("PAGE_BUILDER.ASSETS.DETAILS.NOT_AVAILABLE"));
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
  isCreateFolderPopupOpen.value = true;
}

function closeCreateFolderPopup() {
  isCreateFolderPopupOpen.value = false;
}

async function handleEntryClick(entry: AssetEntry) {
  try {
    await onEntryClick(entry);
  } catch (error) {
    notifyError(error);
  }
}

async function handleCreateFolder(name: string) {
  if (await createAssetFolder(name)) {
    closeCreateFolderPopup();
  }
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

<style lang="scss">
@use "./assetsLibraryContent.scss";
</style>
