<template>
  <VcDataTable
    class="assets-library__table"
    :items="entries"
    :loading="loading"
    :show-all-columns="true"
    :active-item-id="selectedEntryKey"
    :row-actions="tableActionBuilder"
    state-key="page_builder_assets_library"
    @row-click="$emit('entry-click', $event.data.entry)"
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
            empty-icon="lucide-image"
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
      :title="t('ASSET_LIBRARY.TABLE.NAME')"
      :always-visible="true"
      mobile-role="title"
    />

    <VcColumn
      id="type"
      :title="t('ASSET_LIBRARY.TABLE.TYPE')"
      width="20%"
      :always-visible="true"
      mobile-role="field"
    >
      <template #body="{ data }">
        <span v-if="data.isFolder">{{ $t("ASSET_LIBRARY.BADGES.FOLDER") }}</span>
        <span v-else>{{ data.contentType || $t("ASSET_LIBRARY.DETAILS.NOT_AVAILABLE") }}</span>
      </template>
    </VcColumn>

    <VcColumn
      id="size"
      :title="t('ASSET_LIBRARY.TABLE.SIZE')"
      width="14%"
      mobile-role="field"
    >
      <template #body="{ data }">
        <span v-if="data.isFolder">{{ $t("ASSET_LIBRARY.DETAILS.NOT_AVAILABLE") }}</span>
        <span v-else>{{ data.formattedSize }}</span>
      </template>
    </VcColumn>

    <VcColumn
      id="references"
      :title="t('ASSET_LIBRARY.TABLE.REFERENCES')"
      width="14%"
      mobile-role="field"
    >
      <template #body="{ data }">
        <span v-if="data.isBlob">{{ data.referencesCount }}</span>
        <span v-else>{{ $t("ASSET_LIBRARY.DETAILS.NOT_AVAILABLE") }}</span>
      </template>
    </VcColumn>

    <VcColumn
      id="modifiedDate"
      :title="t('ASSET_LIBRARY.TABLE.MODIFIED')"
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
import type { AssetEntry } from "../types";
import type { AssetLibraryEntryViewModel } from "../types";

interface Props {
  entries: AssetLibraryEntryViewModel[];
  selectedEntryKey: string;
  canDelete: boolean;
  loading?: boolean;
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
      icon: "lucide-copy",
      title: t("ASSET_LIBRARY.ACTIONS.COPY_URL"),
      type: "info",
      clickHandler: async () => {
        emit("copy", item.entry);
      },
    });
  }

  if (props.canDelete) {
    actions.push({
      id: "delete",
      icon: "lucide-trash-2",
      title: t("ASSET_LIBRARY.ACTIONS.DELETE"),
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

<style lang="scss" scoped>
.assets-library {
  &__table {
    @apply tw-min-h-0 tw-flex-1;
  }

  &__table-preview {
    @apply tw-flex tw-items-center tw-justify-center;
  }

  &__table-icon {
    @apply tw-text-[32px] tw-text-[color:var(--primary-500)];
  }
}
</style>
