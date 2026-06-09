<template>
  <div class="assets-library__toolbar">
    <div class="assets-library__toolbar-actions">
      <VcButton
        v-if="canCreate"
        variant="secondary"
        icon="material-upload"
        @click="emit('upload')"
      >
        {{ $t("PAGE_BUILDER.ASSETS.TOOLBAR.UPLOAD") }}
      </VcButton>

      <VcButton
        v-if="canCreate"
        variant="secondary"
        icon="material-create_new_folder"
        @click="emit('create-folder')"
      >
        {{ $t("PAGE_BUILDER.ASSETS.TOOLBAR.CREATE_FOLDER") }}
      </VcButton>
    </div>

    <VcInput
      :model-value="searchValue"
      class="assets-library__search"
      clearable
      :placeholder="$t('PAGE_BUILDER.ASSETS.TOOLBAR.SEARCH_PLACEHOLDER')"
      @update:model-value="emit('search-change', ($event as string) || undefined)"
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
        @click="emit('update:viewMode', 'grid')"
      />
      <VcButton
        text
        icon="material-view_list"
        :title="$t('PAGE_BUILDER.ASSETS.VIEW.TABLE')"
        class="assets-library__view-button"
        :class="{ 'assets-library__view-button--active': viewMode === 'table' }"
        @click="emit('update:viewMode', 'table')"
      />
    </div>
  </div>
</template>

<script lang="ts" setup>
import { VcButton, VcHint, VcInput } from "@vc-shell/framework/ui";
import type { AssetLibraryViewMode } from "./assetLibraryTypes";

interface Props {
  canCreate: boolean;
  searchValue?: string;
  totalCount: number;
  viewMode: AssetLibraryViewMode;
}

interface Emits {
  (event: "upload"): void;
  (event: "create-folder"): void;
  (event: "search-change", value: string | undefined): void;
  (event: "update:viewMode", value: AssetLibraryViewMode): void;
}

defineProps<Props>();

const emit = defineEmits<Emits>();
</script>
