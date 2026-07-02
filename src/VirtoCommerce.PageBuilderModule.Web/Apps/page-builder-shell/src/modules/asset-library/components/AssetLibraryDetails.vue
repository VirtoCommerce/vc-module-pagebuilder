<template>
  <aside
    class="assets-library__details"
    @click.stop
  >
    <div class="assets-library__details-header">
      <div class="assets-library__details-title">
        {{ selectedAsset.name }}
      </div>

      <VcButton
        icon="lucide-x"
        text
        @click="$emit('close')"
      />
    </div>

    <div class="assets-library__details-preview">
      <template v-if="selectedAsset.isImage">
        <div class="assets-library__details-image-frame">
          <VcImage
            :src="selectedAsset.previewUrl"
            aspect="3x2"
            background="contain"
            empty-icon="lucide-image"
            class="assets-library__details-image"
          />
        </div>
      </template>
      <template v-else>
        <VcIcon
          :icon="selectedAsset.icon"
          class="assets-library__details-icon"
        />
      </template>
    </div>

    <div class="assets-library__details-section">
      <div class="assets-library__section-title">
        {{ $t("ASSET_LIBRARY.DETAILS.FILE_DETAILS") }}
      </div>

      <div class="assets-library__detail-row">
        <VcHint>{{ $t("ASSET_LIBRARY.DETAILS.SIZE") }}</VcHint>
        <div>{{ selectedAsset.formattedSize }}</div>
      </div>

      <div
        v-if="selectedAsset.dimensions"
        class="assets-library__detail-row"
      >
        <VcHint>{{ $t("ASSET_LIBRARY.DETAILS.DIMENSIONS") }}</VcHint>
        <div>{{ selectedAsset.dimensions }}</div>
      </div>

      <div class="assets-library__detail-row">
        <VcHint>{{ $t("ASSET_LIBRARY.DETAILS.TYPE") }}</VcHint>
        <div>{{ selectedAsset.contentType || $t("ASSET_LIBRARY.DETAILS.NOT_AVAILABLE") }}</div>
      </div>

      <div class="assets-library__detail-row">
        <VcHint>{{ $t("ASSET_LIBRARY.DETAILS.UPLOADED") }}</VcHint>
        <div>{{ selectedAsset.formattedDate }}</div>
      </div>
    </div>

    <div class="assets-library__details-section">
      <div class="assets-library__section-title">
        {{ $t("ASSET_LIBRARY.DETAILS.USED_ON", { count: selectedAsset.referencesCount }) }}
      </div>

      <div
        v-if="selectedAsset.referencePages.length"
        class="assets-library__references-list"
      >
        <div
          v-for="page in selectedAsset.referencePages"
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
        {{ $t("ASSET_LIBRARY.DETAILS.NO_REFERENCES") }}
      </VcHint>
    </div>

    <div class="assets-library__details-actions">
      <VcButton @click="$emit('copy', selectedAsset.entry)">
        {{ $t("ASSET_LIBRARY.DETAILS.COPY_URL") }}
      </VcButton>

      <VcButton
        v-if="canCreate"
        variant="secondary"
        @click="$emit('replace')"
      >
        {{ $t("ASSET_LIBRARY.DETAILS.REPLACE") }}
      </VcButton>

      <VcButton
        v-if="canDelete"
        variant="secondary"
        class="assets-library__delete-button"
        @click="$emit('delete', selectedAsset.entry)"
      >
        {{ $t("ASSET_LIBRARY.DETAILS.DELETE") }}
      </VcButton>
    </div>
  </aside>
</template>

<script lang="ts" setup>
import { VcButton, VcHint, VcIcon, VcImage } from "@vc-shell/framework/ui";
import type { AssetEntry, AssetLibraryDetailsViewModel } from "../types";

interface Props {
  selectedAsset: AssetLibraryDetailsViewModel;
  canCreate: boolean;
  canDelete: boolean;
}

interface Emits {
  (event: "close"): void;
  (event: "replace"): void;
  (event: "copy", entry: AssetEntry): void;
  (event: "delete", entry: AssetEntry): void;
}

defineProps<Props>();

defineEmits<Emits>();
</script>

<style lang="scss" scoped>
.assets-library {
  &__details {
    @apply tw-flex tw-w-full tw-shrink-0 tw-flex-col tw-border-t tw-border-solid tw-border-t-[color:var(--neutrals-200)] tw-bg-[color:var(--additional-50)] tw-p-4 lg:tw-w-[420px] lg:tw-border-l lg:tw-border-t-0 lg:tw-border-l-[color:var(--neutrals-200)];
  }

  &__details-header {
    @apply tw-flex tw-items-start tw-justify-between tw-gap-3;
  }

  &__details-title {
    @apply tw-break-words tw-text-sm tw-font-semibold tw-leading-[18px];
  }

  &__details-preview {
    margin-top: 0.75rem;
    display: flex;
    height: 148px;
    align-items: center;
    justify-content: center;
    overflow: hidden;
    border: 1px solid var(--neutrals-200);
    border-radius: 0.375rem;
    background: var(--additional-50);
  }

  &__details-image-frame {
    display: flex;
    height: 100%;
    width: 100%;
    align-items: center;
    justify-content: center;
    background-color: var(--assets-library-checker-bg);
    background-image:
      linear-gradient(45deg, var(--assets-library-checker-tile) 25%, transparent 25%, transparent 75%, var(--assets-library-checker-tile) 75%, var(--assets-library-checker-tile)),
      linear-gradient(45deg, var(--assets-library-checker-tile) 25%, transparent 25%, transparent 75%, var(--assets-library-checker-tile) 75%, var(--assets-library-checker-tile));
    background-position: 0 0, 8px 8px;
    background-size: 16px 16px;
  }

  &__details-image {
    @apply tw-w-full;
  }

  &__details-icon {
    @apply tw-text-[64px] tw-text-[color:var(--primary-500)];
  }

  &__details-section {
    @apply tw-mt-4 tw-space-y-2;
  }

  &__section-title {
    @apply tw-text-xs tw-font-semibold tw-uppercase tw-text-[color:var(--neutrals-500)];
    letter-spacing: 0;
  }

  &__detail-row {
    @apply tw-space-y-1;
  }

  &__details-actions {
    @apply tw-mt-5 tw-flex tw-flex-col tw-gap-2;
  }

  &__references-list {
    @apply tw-space-y-2;
  }

  &__reference-page {
    @apply tw-rounded tw-border tw-border-solid tw-border-[color:var(--neutrals-200)] tw-bg-[color:var(--additional-50)] tw-p-2;
  }

  &__reference-page-title {
    @apply tw-truncate tw-text-sm tw-font-semibold;
  }

  &__delete-button {
    color: var(--danger-500);
  }
}
</style>
