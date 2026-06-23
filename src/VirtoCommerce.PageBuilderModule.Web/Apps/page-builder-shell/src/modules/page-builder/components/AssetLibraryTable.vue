<template>
  <VcDataTable
    class="assets-library__table"
    :items="entries"
    :show-all-columns="true"
    :active-item-id="selectedEntryKey"
    :row-actions="tableActionBuilder"
    state-key="page_builder_assets_library"
    @row-click="emit('entry-click', $event.data.entry)"
    @click.stop
  >
    <VcColumn
      id="preview"
      title=""
      width="72px"
      :always-visible="true"
      mobile-role="image"
    >
      <template #body="{ data }">
        <div class="assets-library__table-preview">
          <VcImage
            v-if="data.isImage"
            :src="data.previewUrl"
            aspect="1x1"
            size="s"
            background="contain"
            bordered
            empty-icon="material-image"
          />
          <VcIcon
            v-else
            :icon="data.icon"
            class="assets-library__table-icon"
          />
        </div>
      </template>
    </VcColumn>

    <VcColumn
      id="name"
      field="name"
      :title="t('PAGE_BUILDER.ASSETS.TABLE.NAME')"
      :always-visible="true"
      mobile-role="title"
    />

    <VcColumn
      id="type"
      :title="t('PAGE_BUILDER.ASSETS.TABLE.TYPE')"
      width="20%"
      :always-visible="true"
      mobile-role="field"
    >
      <template #body="{ data }">
        <span v-if="data.isFolder">{{ $t("PAGE_BUILDER.ASSETS.BADGES.FOLDER") }}</span>
        <span v-else>{{ data.contentType || $t("PAGE_BUILDER.ASSETS.DETAILS.NOT_AVAILABLE") }}</span>
      </template>
    </VcColumn>

    <VcColumn
      id="size"
      :title="t('PAGE_BUILDER.ASSETS.TABLE.SIZE')"
      width="14%"
      mobile-role="field"
    >
      <template #body="{ data }">
        <span v-if="data.isFolder">{{ $t("PAGE_BUILDER.ASSETS.DETAILS.NOT_AVAILABLE") }}</span>
        <span v-else>{{ data.formattedSize }}</span>
      </template>
    </VcColumn>

    <VcColumn
      id="references"
      :title="t('PAGE_BUILDER.ASSETS.TABLE.REFERENCES')"
      width="14%"
      mobile-role="field"
    >
      <template #body="{ data }">
        <span v-if="data.isBlob">{{ data.referencesCount }}</span>
        <span v-else>{{ $t("PAGE_BUILDER.ASSETS.DETAILS.NOT_AVAILABLE") }}</span>
      </template>
    </VcColumn>

    <VcColumn
      id="modifiedDate"
      :title="t('PAGE_BUILDER.ASSETS.TABLE.MODIFIED')"
      width="18%"
      mobile-role="field"
    >
      <template #body="{ data }">
        {{ data.formattedDate }}
      </template>
    </VcColumn>
  </VcDataTable>
</template>

<script lang="ts" setup>
import { useI18n } from "vue-i18n";
import { VcColumn, VcDataTable, VcIcon, VcImage } from "@vc-shell/framework/ui";
import type { TableAction } from "@vc-shell/framework";
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

function tableActionBuilder(item: AssetLibraryEntryViewModel): TableAction<AssetLibraryEntryViewModel>[] {
  const actions: TableAction<AssetLibraryEntryViewModel>[] = [];

  if (item.isBlob) {
    actions.push({
      id: "copy",
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
      id: "delete",
      icon: "material-delete",
      title: t("PAGE_BUILDER.ASSETS.ACTIONS.DELETE"),
      type: "danger",
      variant: "danger",
      clickHandler: async () => {
        emit("delete", item.entry);
      },
    });
  }

  return actions;
}
</script>
