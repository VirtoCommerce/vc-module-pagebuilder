<template>
  <div class="assets-library__grid">
    <template v-if="showSkeleton">
      <article
        v-for="n in skeletonCount"
        :key="`skeleton-${n}`"
        class="asset-card asset-card--skeleton"
      >
        <div class="asset-card__preview">
          <VcSkeleton
            variant="block"
            width="100%"
            height="100%"
          />
        </div>
        <div class="asset-card__meta">
          <VcSkeleton
            variant="text"
            :rows="1"
          />
          <VcSkeleton
            variant="block"
            width="40%"
            height="12px"
          />
        </div>
      </article>
    </template>
    <template v-else>
      <article
        v-for="item in entries"
        :key="item.key"
        class="asset-card"
        :class="{
          'asset-card--selected': item.selected,
          'asset-card--folder': item.isFolder,
          'asset-card--drop-target': item.dropTarget,
        }"
        @click.stop="$emit('entry-click', item.entry)"
        @dragenter.prevent.stop="item.dropFolderUrl ? $emit('folder-drag', item.entry, $event) : undefined"
        @dragover.prevent.stop="item.dropFolderUrl ? $emit('folder-drag', item.entry, $event) : undefined"
        @dragleave.stop="item.dropFolderUrl ? $emit('folder-drag-leave', item.entry, $event) : undefined"
        @drop.prevent.stop="item.dropFolderUrl ? $emit('folder-drop', item.entry, $event) : undefined"
      >
        <div class="asset-card__actions">
          <button
            v-if="item.isBlob"
            type="button"
            class="asset-card__action"
            :title="$t('ASSET_LIBRARY.ACTIONS.COPY_URL')"
            @click.stop="$emit('copy', item.entry)"
          >
            <VcIcon icon="lucide-copy" />
          </button>
          <button
            v-if="canDelete"
            type="button"
            class="asset-card__action"
            :title="$t('ASSET_LIBRARY.ACTIONS.DELETE')"
            @click.stop="$emit('delete', item.entry)"
          >
            <VcIcon icon="lucide-trash-2" />
          </button>
        </div>

        <div class="asset-card__preview">
          <div
            v-if="item.isBlob"
            class="asset-card__references"
          >
            <template v-if="item.referencesAvailable">
              {{ $t("ASSET_LIBRARY.BADGES.REFERENCES", { count: item.referencesCount }) }}
            </template>
            <template v-else>—</template>
          </div>

          <template v-if="item.isFolder">
            <VcIcon
              icon="lucide-folder"
              class="asset-card__folder-icon"
            />
          </template>
          <template v-else-if="item.isImage">
            <div class="asset-card__image-frame">
              <VcImage
                :src="item.previewUrl"
                aspect="16x9"
                background="contain"
                empty-icon="lucide-image"
                class="asset-card__image"
              />
            </div>
          </template>
          <template v-else>
            <VcIcon
              :icon="item.icon"
              class="asset-card__file-icon"
            />
          </template>
        </div>

        <div class="asset-card__meta">
          <div class="asset-card__title">
            {{ item.name }}
          </div>
          <div class="asset-card__subtitle">
            <span v-if="item.isFolder">{{ $t("ASSET_LIBRARY.BADGES.FOLDER") }}</span>
            <span v-else>{{ item.formattedSize }}</span>
          </div>
        </div>
      </article>
    </template>
  </div>
</template>

<script lang="ts" setup>
import { computed } from "vue";
import { VcIcon, VcImage, VcSkeleton } from "@vc-shell/framework/ui";
import type { AssetEntry, AssetLibraryEntryViewModel } from "../types";

interface Props {
  entries: AssetLibraryEntryViewModel[];
  canDelete: boolean;
  loading?: boolean;
}

interface Emits {
  (event: "entry-click", entry: AssetEntry): void;
  (event: "folder-drag", entry: AssetEntry, dragEvent: DragEvent): void;
  (event: "folder-drag-leave", entry: AssetEntry, dragEvent: DragEvent): void;
  (event: "folder-drop", entry: AssetEntry, dragEvent: DragEvent): void;
  (event: "copy", entry: AssetEntry): void;
  (event: "delete", entry: AssetEntry): void;
}

const props = defineProps<Props>();

defineEmits<Emits>();

const skeletonCount = 8;
const showSkeleton = computed(() => !!props.loading);
</script>

<style lang="scss" scoped>
.assets-library {
  &__grid {
    @apply tw-grid tw-min-h-0 tw-flex-1 tw-gap-3 tw-overflow-auto tw-p-3;
    grid-template-columns: repeat(auto-fill, minmax(min(150px, 100%), 170px));
    grid-auto-rows: max-content;
    align-content: start;
    justify-content: start;
  }
}

.asset-card {
  @apply tw-relative tw-flex tw-cursor-pointer tw-flex-col tw-overflow-hidden tw-rounded-md tw-border tw-border-solid tw-border-[color:var(--neutrals-200)] tw-bg-[color:var(--additional-50)] tw-transition-all;

  &:hover {
    @apply tw--translate-y-[1px] tw-shadow-sm;
  }

  &--selected {
    @apply tw-border-[color:var(--primary-500)] tw-shadow-sm;
  }

  &--drop-target {
    border-color: var(--primary-500);
    box-shadow: 0 0 0 3px color-mix(in srgb, var(--primary-500), transparent 82%);
  }

  &--skeleton {
    @apply tw-cursor-default;
    pointer-events: none;
  }

  &__actions {
    @apply tw-absolute tw-right-2 tw-top-2 tw-z-10 tw-flex tw-gap-1;
  }

  &__action {
    @apply tw-flex tw-h-8 tw-w-8 tw-items-center tw-justify-center tw-rounded-full tw-border-0 tw-bg-[color:var(--additional-50)] tw-p-0 tw-text-xl tw-text-[color:var(--primary-500)] tw-shadow-sm tw-transition-colors;
  }

  &__action:hover,
  &__action:focus-visible {
    background-color: color-mix(in srgb, var(--primary-500), var(--additional-50) 82%);
  }

  &__references {
    @apply tw-absolute tw-left-2 tw-top-2 tw-z-10 tw-rounded-full tw-px-2 tw-py-0.5 tw-text-xs tw-font-semibold tw-text-[color:var(--primary-700)];
    background-color: color-mix(in srgb, var(--primary-500), transparent 84%);
  }

  &__preview {
    display: flex;
    height: 96px;
    align-items: center;
    justify-content: center;
    padding: 0.75rem;
    background-image: var(--assets-library-preview);
  }

  &--folder &__preview {
    @apply tw-bg-[image:var(--assets-library-folder)];
  }

  &__image-frame {
    display: flex;
    height: 100%;
    width: 100%;
    align-items: center;
    justify-content: center;
    border-radius: 0.375rem;
    background-color: var(--assets-library-checker-bg);
    background-image:
      linear-gradient(
        45deg,
        var(--assets-library-checker-tile) 25%,
        transparent 25%,
        transparent 75%,
        var(--assets-library-checker-tile) 75%,
        var(--assets-library-checker-tile)
      ),
      linear-gradient(
        45deg,
        var(--assets-library-checker-tile) 25%,
        transparent 25%,
        transparent 75%,
        var(--assets-library-checker-tile) 75%,
        var(--assets-library-checker-tile)
      );
    background-position:
      0 0,
      8px 8px;
    background-size: 16px 16px;
  }

  &__folder-icon {
    @apply tw-text-[44px] tw-text-white;
  }

  &__file-icon {
    @apply tw-text-[44px] tw-text-[color:var(--primary-500)];
  }

  &__image {
    @apply tw-w-full;
  }

  &__meta {
    @apply tw-space-y-0.5 tw-p-3;
  }

  &__title {
    @apply tw-truncate tw-text-sm tw-font-semibold tw-leading-[18px];
  }

  &__subtitle {
    @apply tw-truncate tw-text-xs tw-leading-[14px] tw-text-[color:var(--neutrals-500)];
  }
}
</style>
