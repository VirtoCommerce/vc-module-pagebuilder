<template>
  <div class="assets-library__toolbar">
    <div class="assets-library__toolbar-actions">
      <VcButton
        v-if="canCreate"
        variant="secondary"
        icon="lucide-upload"
        @click="$emit('upload')"
      >
        {{ $t("ASSET_LIBRARY.TOOLBAR.UPLOAD") }}
      </VcButton>

      <VcButton
        v-if="canCreate"
        variant="secondary"
        icon="lucide-folder-plus"
        @click="$emit('create-folder')"
      >
        {{ $t("ASSET_LIBRARY.TOOLBAR.CREATE_FOLDER") }}
      </VcButton>
    </div>

    <VcInput
      :model-value="searchValue"
      class="assets-library__search"
      clearable
      :placeholder="$t('ASSET_LIBRARY.TOOLBAR.SEARCH_PLACEHOLDER')"
      @update:model-value="$emit('search-change', ($event as string) || undefined)"
    />

    <VcHint class="assets-library__counter">
      {{ $t("ASSET_LIBRARY.COUNTER", { count: totalCount }) }}
    </VcHint>

    <VcButtonGroup
      attached
      class="assets-library__view-toggle"
    >
      <VcButton
        icon="lucide-layout-grid"
        icon-size="l"
        :selected="viewMode === 'grid'"
        :title="$t('ASSET_LIBRARY.VIEW.GRID')"
        variant="outline"
        @click="viewMode = 'grid'"
      />
      <VcButton
        icon="lucide-list"
        icon-size="l"
        :selected="viewMode === 'table'"
        :title="$t('ASSET_LIBRARY.VIEW.TABLE')"
        variant="outline"
        @click="viewMode = 'table'"
      />
    </VcButtonGroup>
  </div>
</template>

<script lang="ts" setup>
import { VcButton, VcButtonGroup, VcHint, VcInput } from "@vc-shell/framework/ui";
import type { AssetLibraryViewMode } from "../types";

interface Props {
  canCreate: boolean;
  searchValue?: string;
  totalCount: number;
}

interface Emits {
  (event: "upload"): void;
  (event: "create-folder"): void;
  (event: "search-change", value: string | undefined): void;
}

const viewMode = defineModel<AssetLibraryViewMode>("viewMode");

defineProps<Props>();

defineEmits<Emits>();
</script>

<style lang="scss" scoped>
.assets-library {
  &__toolbar {
    @apply tw-flex tw-flex-wrap tw-items-center tw-gap-2 tw-border-b tw-border-solid tw-border-b-[color:var(--neutrals-200)] tw-bg-[color:var(--additional-50)] tw-p-3;
  }

  &__toolbar-actions {
    @apply tw-flex tw-items-center tw-gap-2;
  }

  &__search {
    @apply tw-min-w-[240px] tw-grow;
  }

  &__counter {
    @apply tw-whitespace-nowrap tw-text-[color:var(--neutrals-500)];
  }

  &__view-toggle {
    @apply tw-ml-auto;
  }
}
</style>
