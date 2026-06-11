<template>
  <VcTable
    class="assets-library__table"
    :items="entries"
    :columns="tableColumns"
    :expanded="true"
    :header="false"
    :footer="false"
    :selected-item-id="selectedEntryKey"
    :enable-item-actions="true"
    :item-action-builder="tableActionBuilder"
    state-key="page_builder_assets_library"
    @item-click="emit('entry-click', $event.entry)"
    @click.stop
  >
    <template #item_preview="{ item }">
      <div class="assets-library__table-preview">
        <VcImage
          v-if="item.isImage"
          :src="item.previewUrl"
          aspect="1x1"
          size="s"
          background="contain"
          bordered
          empty-icon="material-image"
        />
        <VcIcon
          v-else
          :icon="item.icon"
          class="assets-library__table-icon"
        />
      </div>
    </template>

    <template #item_type="{ item }">
      <span v-if="item.isFolder">{{ $t("PAGE_BUILDER.ASSETS.BADGES.FOLDER") }}</span>
      <span v-else>{{ item.contentType || $t("PAGE_BUILDER.ASSETS.DETAILS.NOT_AVAILABLE") }}</span>
    </template>

    <template #item_size="{ item }">
      <span v-if="item.isFolder">{{ $t("PAGE_BUILDER.ASSETS.DETAILS.NOT_AVAILABLE") }}</span>
      <span v-else>{{ item.formattedSize }}</span>
    </template>

    <template #item_references="{ item }">
      <span v-if="item.isBlob">{{ item.referencesCount }}</span>
      <span v-else>{{ $t("PAGE_BUILDER.ASSETS.DETAILS.NOT_AVAILABLE") }}</span>
    </template>

    <template #item_modifiedDate="{ item }">
      {{ item.formattedDate }}
    </template>

    <template #mobile-item="{ item }">
      <div class="assets-library__table-mobile-item">
        <div class="assets-library__table-preview">
          <VcImage
            v-if="item.isImage"
            :src="item.previewUrl"
            aspect="1x1"
            size="s"
            background="contain"
            bordered
            empty-icon="material-image"
          />
          <VcIcon
            v-else
            :icon="item.icon"
            class="assets-library__table-icon"
          />
        </div>
        <div class="assets-library__table-mobile-meta">
          <div class="assets-library__table-mobile-title">{{ item.name }}</div>
          <VcHint>
            <span v-if="item.isFolder">{{ $t("PAGE_BUILDER.ASSETS.BADGES.FOLDER") }}</span>
            <span v-else>{{ item.formattedSize }}</span>
          </VcHint>
        </div>
      </div>
    </template>
  </VcTable>
</template>

<script lang="ts" setup>
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { VcHint, VcIcon, VcImage, VcTable } from "@vc-shell/framework/ui";
import type { IActionBuilderResult, ITableColumns } from "@vc-shell/framework";
import type { AssetEntry } from "../composables/useAssetsLibraryApi";
import type { AssetLibraryEntryViewModel } from "./assetLibraryTypes";

interface Props {
  entries: AssetLibraryEntryViewModel[];
  selectedEntryKey: string;
  canDelete: boolean;
}

interface Emits {
  (event: "entry-click", entry: AssetEntry): void;
  (event: "copy", entry: AssetEntry): void;
  (event: "delete", entry: AssetEntry): void;
}

const props = defineProps<Props>();
const emit = defineEmits<Emits>();
const { t } = useI18n({ useScope: "global" });

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

function tableActionBuilder(item: AssetLibraryEntryViewModel): IActionBuilderResult<AssetLibraryEntryViewModel>[] {
  const actions: IActionBuilderResult<AssetLibraryEntryViewModel>[] = [];

  if (item.isBlob) {
    actions.push({
      icon: "material-content_copy",
      title: t("PAGE_BUILDER.ASSETS.ACTIONS.COPY_URL"),
      type: "info",
      clickHandler: async () => {
        emit("copy", item.entry);
      },
    });
  }

  if (props.canDelete) {
    actions.push({
      icon: "material-delete",
      title: t("PAGE_BUILDER.ASSETS.ACTIONS.DELETE"),
      type: "danger",
      clickHandler: async () => {
        emit("delete", item.entry);
      },
    });
  }

  return actions;
}
</script>
