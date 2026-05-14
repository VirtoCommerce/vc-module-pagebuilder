<template>
  <div
    v-loading="loading"
    class="assets-library tw-flex tw-h-full tw-flex-col"
  >
    <div class="assets-library__toolbar">
      <div class="assets-library__toolbar-actions">
        <VcButton
          v-if="canCreate"
          icon="material-upload"
          @click="openUploadDialog"
        >
          {{ $t("PAGE_BUILDER.ASSETS.TOOLBAR.UPLOAD") }}
        </VcButton>

        <VcButton
          v-if="canCreate"
          variant="secondary"
          icon="material-create_new_folder"
          @click="openCreateFolderPopup"
        >
          {{ $t("PAGE_BUILDER.ASSETS.TOOLBAR.CREATE_FOLDER") }}
        </VcButton>
      </div>

      <VcInput
        :model-value="searchValue"
        class="assets-library__search"
        clearable
        :placeholder="$t('PAGE_BUILDER.ASSETS.TOOLBAR.SEARCH_PLACEHOLDER')"
        @update:model-value="onSearchChange"
      />

      <VcHint class="assets-library__counter">
        {{ $t("PAGE_BUILDER.ASSETS.COUNTER", { count: totalCount }) }}
      </VcHint>

      <div class="assets-library__view-toggle">
        <VcButton
          text
          icon="material-grid_view"
          :title="$t('PAGE_BUILDER.ASSETS.VIEW.GRID')"
          class="assets-library__view-button"
          :class="{ 'assets-library__view-button--active': viewMode === 'grid' }"
          @click="viewMode = 'grid'"
        />
        <VcButton
          text
          icon="material-view_list"
          :title="$t('PAGE_BUILDER.ASSETS.VIEW.TABLE')"
          class="assets-library__view-button"
          :class="{ 'assets-library__view-button--active': viewMode === 'table' }"
          @click="viewMode = 'table'"
        />
      </div>
    </div>

    <div class="assets-library__body">
      <section
        class="assets-library__content"
        :class="{ 'assets-library__content--drag-over': isDraggingOverSurface }"
        @click="clearSelection"
        @dragenter.prevent="handleSurfaceDragEnter"
        @dragover.prevent="handleSurfaceDragOver"
        @dragleave="handleSurfaceDragLeave"
        @drop.prevent="handleSurfaceDrop"
      >
        <div
          class="assets-library__breadcrumbs"
          @click.stop
        >
          <VcBreadcrumbs :items="breadcrumbs" />
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

        <div
          v-if="entries.length && viewMode === 'grid'"
          class="assets-library__grid"
        >
          <article
            v-for="entry in entries"
            :key="getEntryKey(entry)"
            class="asset-card"
            :class="{
              'asset-card--selected': isEntrySelected(entry),
              'asset-card--folder': entry.type === 'folder',
              'asset-card--drop-target': entry.type === 'folder' && draggedFolderUrl === getEntryDropFolderUrl(entry),
            }"
            @click.stop="handleEntryClick(entry)"
            @dragenter.prevent.stop="handleFolderDragEnter(entry, $event)"
            @dragover.prevent.stop="handleFolderDragOver(entry, $event)"
            @dragleave.stop="handleFolderDragLeave(entry, $event)"
            @drop.prevent.stop="handleFolderDrop(entry, $event)"
          >
            <div class="asset-card__actions">
              <VcButton
                v-if="entry.type === 'blob'"
                icon="material-content_copy"
                text
                @click.stop="copyAssetUrl(entry)"
              />
              <VcButton
                v-if="canDelete"
                icon="material-delete"
                text
                @click.stop="confirmDelete(entry)"
              />
            </div>

            <div class="asset-card__preview">
              <div
                v-if="entry.type === 'blob'"
                class="asset-card__references"
              >
                {{ $t("PAGE_BUILDER.ASSETS.BADGES.REFERENCES", { count: getReferencesCount(entry) }) }}
              </div>

              <template v-if="entry.type === 'folder'">
                <VcIcon
                  icon="material-folder"
                  class="asset-card__folder-icon"
                />
              </template>
              <template v-else-if="isImage(entry)">
                <div class="asset-card__image-frame">
                  <VcImage
                    :src="getPreviewUrl(entry)"
                    aspect="16x9"
                    background="contain"
                    empty-icon="material-image"
                    class="asset-card__image"
                  />
                </div>
              </template>
              <template v-else>
                <VcIcon
                  :icon="getEntryIcon(entry)"
                  class="asset-card__file-icon"
                />
              </template>
            </div>

            <div class="asset-card__meta">
              <div class="asset-card__title">
                {{ entry.name }}
              </div>
              <div class="asset-card__subtitle">
                <span v-if="entry.type === 'folder'">{{ $t("PAGE_BUILDER.ASSETS.BADGES.FOLDER") }}</span>
                <span v-else>{{ formatFileSize(entry.size) }}</span>
              </div>
            </div>
          </article>
        </div>

        <!-- @vue-generic {AssetEntry} -->
        <VcTable
          v-else-if="entries.length"
          class="assets-library__table"
          :items="entries"
          :columns="tableColumns"
          :expanded="true"
          :header="false"
          :footer="false"
          :loading="loading"
          :selected-item-id="selectedEntryId"
          :enable-item-actions="true"
          :item-action-builder="tableActionBuilder"
          state-key="page_builder_assets_library"
          @item-click="handleEntryClick"
          @click.stop
        >
          <template #item_preview="{ item }">
            <div class="assets-library__table-preview">
              <VcImage
                v-if="isImage(item)"
                :src="getPreviewUrl(item)"
                aspect="1x1"
                size="s"
                background="contain"
                bordered
                empty-icon="material-image"
              />
              <VcIcon
                v-else
                :icon="getEntryIcon(item)"
                class="assets-library__table-icon"
              />
            </div>
          </template>

          <template #item_type="{ item }">
            <span v-if="item.type === 'folder'">{{ $t("PAGE_BUILDER.ASSETS.BADGES.FOLDER") }}</span>
            <span v-else>{{ item.contentType || $t("PAGE_BUILDER.ASSETS.DETAILS.NOT_AVAILABLE") }}</span>
          </template>

          <template #item_size="{ item }">
            <span v-if="item.type === 'folder'">{{ $t("PAGE_BUILDER.ASSETS.DETAILS.NOT_AVAILABLE") }}</span>
            <span v-else>{{ formatFileSize(item.size) }}</span>
          </template>

          <template #item_references="{ item }">
            <span v-if="item.type === 'blob'">{{ getReferencesCount(item) }}</span>
            <span v-else>{{ $t("PAGE_BUILDER.ASSETS.DETAILS.NOT_AVAILABLE") }}</span>
          </template>

          <template #item_modifiedDate="{ item }">
            {{ formatDate(item.modifiedDate || item.createdDate) }}
          </template>

          <template #mobile-item="{ item }">
            <div class="assets-library__table-mobile-item">
              <div class="assets-library__table-preview">
                <VcImage
                  v-if="isImage(item)"
                  :src="getPreviewUrl(item)"
                  aspect="1x1"
                  size="s"
                  background="contain"
                  bordered
                  empty-icon="material-image"
                />
                <VcIcon
                  v-else
                  :icon="getEntryIcon(item)"
                  class="assets-library__table-icon"
                />
              </div>
              <div class="assets-library__table-mobile-meta">
                <div class="assets-library__table-mobile-title">{{ item.name }}</div>
                <VcHint>
                  <span v-if="item.type === 'folder'">{{ $t("PAGE_BUILDER.ASSETS.BADGES.FOLDER") }}</span>
                  <span v-else>{{ formatFileSize(item.size) }}</span>
                </VcHint>
              </div>
            </div>
          </template>
        </VcTable>

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
            @upload="handleUploadFiles"
          />
        </div>
      </section>

      <aside
        v-if="selectedAsset"
        class="assets-library__details"
        @click.stop
      >
        <div class="assets-library__details-header">
          <div class="assets-library__details-title">
            {{ selectedAsset.name }}
          </div>

          <VcButton
            icon="material-close"
            text
            @click="clearSelection"
          />
        </div>

        <div class="assets-library__details-preview">
          <template v-if="isImage(selectedAsset)">
            <div class="assets-library__details-image-frame">
              <VcImage
                :src="getPreviewUrl(selectedAsset)"
                aspect="3x2"
                background="contain"
                empty-icon="material-image"
                class="assets-library__details-image"
              />
            </div>
          </template>
          <template v-else>
            <VcIcon
              :icon="getEntryIcon(selectedAsset)"
              class="assets-library__details-icon"
            />
          </template>
        </div>

        <div class="assets-library__details-section">
          <div class="assets-library__section-title">
            {{ $t("PAGE_BUILDER.ASSETS.DETAILS.FILE_DETAILS") }}
          </div>

          <div class="assets-library__detail-row">
            <VcHint>{{ $t("PAGE_BUILDER.ASSETS.DETAILS.SIZE") }}</VcHint>
            <div>{{ formatFileSize(selectedAsset.size) }}</div>
          </div>

          <div
            v-if="selectedAssetDimensions"
            class="assets-library__detail-row"
          >
            <VcHint>{{ $t("PAGE_BUILDER.ASSETS.DETAILS.DIMENSIONS") }}</VcHint>
            <div>{{ selectedAssetDimensions }}</div>
          </div>

          <div class="assets-library__detail-row">
            <VcHint>{{ $t("PAGE_BUILDER.ASSETS.DETAILS.TYPE") }}</VcHint>
            <div>{{ selectedAsset.contentType || $t("PAGE_BUILDER.ASSETS.DETAILS.NOT_AVAILABLE") }}</div>
          </div>

          <div class="assets-library__detail-row">
            <VcHint>{{ $t("PAGE_BUILDER.ASSETS.DETAILS.UPLOADED") }}</VcHint>
            <div>{{ formatDate(selectedAsset.createdDate || selectedAsset.modifiedDate) }}</div>
          </div>
        </div>

        <div class="assets-library__details-section">
          <div class="assets-library__section-title">
            {{ $t("PAGE_BUILDER.ASSETS.DETAILS.PATH") }}
          </div>
          <VcLink
            class="assets-library__path"
            @click="copyAssetUrl(selectedAsset)"
          >
            {{ getAssetPath(selectedAsset) }}
          </VcLink>
        </div>

        <div class="assets-library__details-section">
          <div class="assets-library__section-title">
            {{ $t("PAGE_BUILDER.ASSETS.DETAILS.USED_ON", { count: getReferencesCount(selectedAsset) }) }}
          </div>

          <div
            v-if="getReferencePages(selectedAsset).length"
            class="assets-library__references-list"
          >
            <div
              v-for="page in getReferencePages(selectedAsset)"
              :key="page.id || page.permalink || page.name"
              class="assets-library__reference-page"
            >
              <div class="assets-library__reference-page-title">
                {{ page.name || page.permalink || page.id }}
              </div>
              <VcHint v-if="page.cultureName || page.status">
                {{ [page.cultureName, page.status].filter(Boolean).join(" - ") }}
              </VcHint>
            </div>
          </div>
          <VcHint v-else>
            {{ $t("PAGE_BUILDER.ASSETS.DETAILS.NO_REFERENCES") }}
          </VcHint>
        </div>

        <div class="assets-library__details-actions">
          <VcButton
            v-if="canCreate"
            @click="openReplaceDialog"
          >
            {{ $t("PAGE_BUILDER.ASSETS.DETAILS.REPLACE") }}
          </VcButton>

          <VcButton
            variant="secondary"
            @click="copyAssetUrl(selectedAsset)"
          >
            {{ $t("PAGE_BUILDER.ASSETS.DETAILS.COPY_URL") }}
          </VcButton>

          <VcButton
            v-if="canDelete"
            variant="secondary"
            class="assets-library__delete-button"
            @click="confirmDelete(selectedAsset)"
          >
            {{ $t("PAGE_BUILDER.ASSETS.DETAILS.DELETE") }}
          </VcButton>
        </div>
      </aside>
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
import { IActionBuilderResult, ITableColumns, notification, usePermissions, usePopup } from "@vc-shell/framework";
import { AssetEntry } from "../composables/useAssetsLibraryApi";
import { useAssetsLibrary } from "../composables/useAssetsLibrary";
import CreateFolderPopup from "./CreateFolderPopup.vue";

export interface ExposedAssetsLibraryContent {
  reload: () => Promise<void>;
}

const { t } = useI18n({ useScope: "global" });
const { hasAccess } = usePermissions();
const { showConfirmation } = usePopup();

const replaceInputRef = ref<HTMLInputElement | null>(null);
const isCreateFolderPopupOpen = ref(false);
const isUploadPopupOpen = ref(false);
const viewMode = ref<"grid" | "table">("grid");
const isDraggingOverSurface = ref(false);
const draggedFolderUrl = ref<string>();

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
  formatFileSize,
  formatDate,
  getAssetPath,
  getAssetPublicUrl,
  getPreviewUrl,
  createFolder,
  uploadFiles,
  replaceSelectedAsset,
  deleteEntry,
} = useAssetsLibrary(t);

const canCreate = computed(() => hasAccess("platform:asset:create"));
const canDelete = computed(() => hasAccess("platform:asset:delete"));
const selectedEntryId = computed(() => getEntryKey(selectedAsset.value));

const tableColumns = computed((): ITableColumns[] => [
  {
    id: "preview",
    title: "",
    width: "72px",
    alwaysVisible: true,
  },
  {
    id: "name",
    title: t("PAGE_BUILDER.ASSETS.TABLE.NAME"),
    alwaysVisible: true,
  },
  {
    id: "type",
    title: t("PAGE_BUILDER.ASSETS.TABLE.TYPE"),
    width: "20%",
    alwaysVisible: true,
  },
  {
    id: "size",
    title: t("PAGE_BUILDER.ASSETS.TABLE.SIZE"),
    width: "14%",
  },
  {
    id: "references",
    title: t("PAGE_BUILDER.ASSETS.TABLE.REFERENCES"),
    width: "14%",
  },
  {
    id: "modifiedDate",
    title: t("PAGE_BUILDER.ASSETS.TABLE.MODIFIED"),
    width: "18%",
  },
]);

const onSearchChange = debounce(async (keyword: string | undefined) => {
  searchValue.value = keyword;

  try {
    await reload();
  } catch (error) {
    notification.error(getErrorMessage(error));
  }
}, 350);

function getErrorMessage(error: unknown): string {
  return error instanceof Error ? error.message : t("PAGE_BUILDER.ASSETS.NOTIFICATIONS.ERROR_GENERIC");
}

function hasDraggedFiles(event: DragEvent): boolean {
  return Array.from(event.dataTransfer?.types ?? []).includes("Files");
}

function getEntryKey(entry: AssetEntry | undefined): string {
  return entry?.relativeUrl || entry?.url || entry?.name || "";
}

function isEntrySelected(entry: AssetEntry): boolean {
  return !!selectedAsset.value && getEntryKey(selectedAsset.value) === getEntryKey(entry);
}

function markDropEffect(event: DragEvent) {
  if (event.dataTransfer) {
    event.dataTransfer.dropEffect = canCreate.value && hasDraggedFiles(event) ? "copy" : "none";
  }
}

function isLeavingCurrentTarget(event: DragEvent): boolean {
  const currentTarget = event.currentTarget as HTMLElement | null;
  const nextTarget = event.relatedTarget;

  return !!currentTarget && nextTarget instanceof Node && currentTarget.contains(nextTarget);
}

function resetDragState() {
  isDraggingOverSurface.value = false;
  draggedFolderUrl.value = undefined;
}

function getEntryDropFolderUrl(entry: AssetEntry): string | undefined {
  if (entry.type !== "folder") {
    return undefined;
  }

  return entry.relativeUrl || entry.url;
}

function handleSurfaceDragEnter(event: DragEvent) {
  if (!canCreate.value || !hasDraggedFiles(event)) {
    return;
  }

  markDropEffect(event);
  isDraggingOverSurface.value = true;
}

function handleSurfaceDragOver(event: DragEvent) {
  if (!canCreate.value || !hasDraggedFiles(event)) {
    return;
  }

  markDropEffect(event);
  isDraggingOverSurface.value = true;
}

function handleSurfaceDragLeave(event: DragEvent) {
  if (isLeavingCurrentTarget(event)) {
    return;
  }

  resetDragState();
}

async function handleSurfaceDrop(event: DragEvent) {
  const files = event.dataTransfer?.files;
  resetDragState();

  await uploadDroppedFiles(files);
}

function handleFolderDragEnter(entry: AssetEntry, event: DragEvent) {
  const folderUrl = getEntryDropFolderUrl(entry);

  if (!canCreate.value || !hasDraggedFiles(event)) {
    return;
  }

  markDropEffect(event);
  isDraggingOverSurface.value = true;
  draggedFolderUrl.value = folderUrl;
}

function handleFolderDragOver(entry: AssetEntry, event: DragEvent) {
  const folderUrl = getEntryDropFolderUrl(entry);

  if (!canCreate.value || !hasDraggedFiles(event)) {
    return;
  }

  markDropEffect(event);
  isDraggingOverSurface.value = true;
  draggedFolderUrl.value = folderUrl;
}

function handleFolderDragLeave(entry: AssetEntry, event: DragEvent) {
  if (isLeavingCurrentTarget(event)) {
    return;
  }

  if (draggedFolderUrl.value === getEntryDropFolderUrl(entry)) {
    draggedFolderUrl.value = undefined;
  }
}

async function handleFolderDrop(entry: AssetEntry, event: DragEvent) {
  const files = event.dataTransfer?.files;
  const folderUrl = getEntryDropFolderUrl(entry);
  resetDragState();

  await uploadDroppedFiles(files, folderUrl);
}

async function uploadDroppedFiles(files: FileList | undefined, folderUrl?: string) {
  await handleUploadFiles(files, folderUrl);
}

async function handleUploadFiles(files: FileList | File[] | undefined, folderUrl?: string): Promise<boolean> {
  if (!canCreate.value || !files?.length) {
    return false;
  }

  try {
    await uploadFiles(files, folderUrl);
    notification.success(t("PAGE_BUILDER.ASSETS.NOTIFICATIONS.UPLOADED"));
    return true;
  } catch (error) {
    notification.error(getErrorMessage(error));
    return false;
  }
}

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
    notification.error(getErrorMessage(error));
  }
}

async function handleCreateFolder(name: string) {
  const value = name.trim();

  if (!value) {
    return;
  }

  try {
    await createFolder(value);
    closeCreateFolderPopup();
    notification.success(t("PAGE_BUILDER.ASSETS.NOTIFICATIONS.FOLDER_CREATED"));
  } catch (error) {
    notification.error(getErrorMessage(error));
  }
}

async function onReplaceChange(event: Event) {
  const input = event.target as HTMLInputElement;
  const replacement = input.files?.[0];

  if (!replacement) {
    input.value = "";
    return;
  }

  try {
    await replaceSelectedAsset(replacement);
    notification.success(t("PAGE_BUILDER.ASSETS.NOTIFICATIONS.REPLACED"));
  } catch (error) {
    notification.error(getErrorMessage(error));
  } finally {
    input.value = "";
  }
}

async function handlePopupUpload(files: FileList) {
  const uploaded = await handleUploadFiles(files);

  if (uploaded) {
    closeUploadPopup();
  }
}

async function copyAssetUrl(entry: AssetEntry) {
  const value = getAssetPublicUrl(entry) || entry.url || entry.relativeUrl;

  if (!value) {
    return;
  }

  try {
    await navigator.clipboard.writeText(value);
  } catch {
    window.prompt(t("PAGE_BUILDER.ASSETS.DETAILS.URL"), value);
  }

  notification.success(t("PAGE_BUILDER.ASSETS.NOTIFICATIONS.URL_COPIED"));
}

async function confirmDelete(entry: AssetEntry) {
  const confirmed = await showConfirmation(
    t("PAGE_BUILDER.ASSETS.CONFIRM.DELETE_SINGLE", { name: entry.name }),
  );

  if (!confirmed) {
    return;
  }

  try {
    await deleteEntry(entry);
    notification.success(t("PAGE_BUILDER.ASSETS.NOTIFICATIONS.DELETED"));
  } catch (error) {
    notification.error(getErrorMessage(error));
  }
}

function tableActionBuilder(entry: AssetEntry): IActionBuilderResult<AssetEntry>[] {
  const actions: IActionBuilderResult<AssetEntry>[] = [];

  if (entry.type === "blob") {
    actions.push({
      icon: "material-content_copy",
      title: t("PAGE_BUILDER.ASSETS.ACTIONS.COPY_URL"),
      type: "info",
      clickHandler: async () => {
        await copyAssetUrl(entry);
      },
    });
  }

  if (canDelete.value) {
    actions.push({
      icon: "material-delete",
      title: t("PAGE_BUILDER.ASSETS.ACTIONS.DELETE"),
      type: "danger",
      clickHandler: async () => {
        await confirmDelete(entry);
      },
    });
  }

  return actions;
}

async function reloadContent() {
  try {
    await reload();
  } catch (error) {
    notification.error(getErrorMessage(error));
  }
}

onMounted(async () => {
  try {
    await initialize();
  } catch (error) {
    notification.error(getErrorMessage(error));
  }
});

defineExpose<ExposedAssetsLibraryContent>({
  reload: reloadContent,
});
</script>

<style lang="scss" scoped>
@use "./assetsLibraryContent.scss";
</style>
