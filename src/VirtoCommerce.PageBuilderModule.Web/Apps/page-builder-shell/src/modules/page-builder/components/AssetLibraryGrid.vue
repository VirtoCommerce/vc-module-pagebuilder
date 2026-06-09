<template>
  <div class="assets-library__grid">
    <article
      v-for="item in entries"
      :key="item.key"
      class="asset-card"
      :class="{
        'asset-card--selected': item.selected,
        'asset-card--folder': item.isFolder,
        'asset-card--drop-target': item.dropTarget,
      }"
      @click.stop="emit('entry-click', item.entry)"
      @dragenter.prevent.stop="item.dropFolderUrl ? emit('folder-drag', item.entry, $event) : undefined"
      @dragover.prevent.stop="item.dropFolderUrl ? emit('folder-drag', item.entry, $event) : undefined"
      @dragleave.stop="item.dropFolderUrl ? emit('folder-drag-leave', item.entry, $event) : undefined"
      @drop.prevent.stop="item.dropFolderUrl ? emit('folder-drop', item.entry, $event) : undefined"
    >
      <div class="asset-card__actions">
        <VcButton
          v-if="item.isBlob"
          icon="material-content_copy"
          text
          @click.stop="emit('copy', item.entry)"
        />
        <VcButton
          v-if="canDelete"
          icon="material-delete"
          text
          @click.stop="emit('delete', item.entry)"
        />
      </div>

      <div class="asset-card__preview">
        <div
          v-if="item.isBlob"
          class="asset-card__references"
        >
          {{ $t("PAGE_BUILDER.ASSETS.BADGES.REFERENCES", { count: item.referencesCount }) }}
        </div>

        <template v-if="item.isFolder">
          <VcIcon
            icon="material-folder"
            class="asset-card__folder-icon"
          />
        </template>
        <template v-else-if="item.isImage">
          <div class="asset-card__image-frame">
            <VcImage
              :src="item.previewUrl"
              aspect="16x9"
              background="contain"
              empty-icon="material-image"
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
          <span v-if="item.isFolder">{{ $t("PAGE_BUILDER.ASSETS.BADGES.FOLDER") }}</span>
          <span v-else>{{ item.formattedSize }}</span>
        </div>
      </div>
    </article>
  </div>
</template>

<script lang="ts" setup>
import { VcButton, VcIcon, VcImage } from "@vc-shell/framework/ui";
import type { AssetEntry } from "../composables/useAssetsLibraryApi";
import type { AssetLibraryEntryViewModel } from "./assetLibraryTypes";

interface Props {
  entries: AssetLibraryEntryViewModel[];
  canDelete: boolean;
}

interface Emits {
  (event: "entry-click", entry: AssetEntry): void;
  (event: "folder-drag", entry: AssetEntry, dragEvent: DragEvent): void;
  (event: "folder-drag-leave", entry: AssetEntry, dragEvent: DragEvent): void;
  (event: "folder-drop", entry: AssetEntry, dragEvent: DragEvent): void;
  (event: "copy", entry: AssetEntry): void;
  (event: "delete", entry: AssetEntry): void;
}

defineProps<Props>();

const emit = defineEmits<Emits>();
</script>
