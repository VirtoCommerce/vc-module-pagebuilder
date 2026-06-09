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
        icon="material-close"
        text
        @click="emit('close')"
      />
    </div>

    <div class="assets-library__details-preview">
      <template v-if="selectedAsset.isImage">
        <div class="assets-library__details-image-frame">
          <VcImage
            :src="selectedAsset.previewUrl"
            aspect="3x2"
            background="contain"
            empty-icon="material-image"
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
        {{ $t("PAGE_BUILDER.ASSETS.DETAILS.FILE_DETAILS") }}
      </div>

      <div class="assets-library__detail-row">
        <VcHint>{{ $t("PAGE_BUILDER.ASSETS.DETAILS.SIZE") }}</VcHint>
        <div>{{ selectedAsset.formattedSize }}</div>
      </div>

      <div
        v-if="selectedAsset.dimensions"
        class="assets-library__detail-row"
      >
        <VcHint>{{ $t("PAGE_BUILDER.ASSETS.DETAILS.DIMENSIONS") }}</VcHint>
        <div>{{ selectedAsset.dimensions }}</div>
      </div>

      <div class="assets-library__detail-row">
        <VcHint>{{ $t("PAGE_BUILDER.ASSETS.DETAILS.TYPE") }}</VcHint>
        <div>{{ selectedAsset.contentType || $t("PAGE_BUILDER.ASSETS.DETAILS.NOT_AVAILABLE") }}</div>
      </div>

      <div class="assets-library__detail-row">
        <VcHint>{{ $t("PAGE_BUILDER.ASSETS.DETAILS.UPLOADED") }}</VcHint>
        <div>{{ selectedAsset.formattedDate }}</div>
      </div>
    </div>

    <div class="assets-library__details-section">
      <div class="assets-library__section-title">
        {{ $t("PAGE_BUILDER.ASSETS.DETAILS.USED_ON", { count: selectedAsset.referencesCount }) }}
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
        {{ $t("PAGE_BUILDER.ASSETS.DETAILS.NO_REFERENCES") }}
      </VcHint>
    </div>

    <div class="assets-library__details-actions">
      <VcButton @click="emit('copy', selectedAsset.entry)">
        {{ $t("PAGE_BUILDER.ASSETS.DETAILS.COPY_URL") }}
      </VcButton>

      <VcButton
        v-if="canCreate"
        variant="secondary"
        @click="emit('replace')"
      >
        {{ $t("PAGE_BUILDER.ASSETS.DETAILS.REPLACE") }}
      </VcButton>

      <VcButton
        v-if="canDelete"
        variant="secondary"
        class="assets-library__delete-button"
        @click="emit('delete', selectedAsset.entry)"
      >
        {{ $t("PAGE_BUILDER.ASSETS.DETAILS.DELETE") }}
      </VcButton>
    </div>
  </aside>
</template>

<script lang="ts" setup>
import { VcButton, VcHint, VcIcon, VcImage } from "@vc-shell/framework/ui";
import type { AssetEntry } from "../composables/useAssetsLibraryApi";
import type { AssetLibraryDetailsViewModel } from "./assetLibraryTypes";

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

const emit = defineEmits<Emits>();
</script>
