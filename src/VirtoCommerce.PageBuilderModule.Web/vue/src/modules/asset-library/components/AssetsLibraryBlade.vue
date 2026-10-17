<template>
  <VcBlade
    width="100%"
    :title="bladeTitle"
    :toolbar-items="bladeToolbar"
  >
    <AssetsLibraryContent ref="assetsLibraryRef" />
  </VcBlade>
</template>

<script lang="ts" setup>
import { computed, useTemplateRef } from "vue";
import { useI18n } from "vue-i18n";
import { IBladeToolbar, useBlade } from "@vc-shell/framework";
import { VcBlade } from "@vc-shell/framework/ui";
import AssetsLibraryContent, { type ExposedAssetsLibraryContent } from "./AssetsLibraryContent.vue";

// The Assets Library blade's body, defined as a blade by the standalone app (AssetsLibrary, a workspace)
// and by the Admin Shell UI (PageBuilderAssets, opened from a store's pages).

const { exposeToChildren } = useBlade();
const { t } = useI18n({ useScope: "global" });
const assetsLibraryRef = useTemplateRef<ExposedAssetsLibraryContent>("assetsLibraryRef");
const bladeTitle = computed(() => t("ASSET_LIBRARY.TITLE"));

const bladeToolbar = computed((): IBladeToolbar[] => [
  {
    id: "refresh",
    title: t("ASSET_LIBRARY.TOOLBAR.REFRESH"),
    icon: "lucide-refresh-cw",
    clickHandler: async () => { await reload(); },
  },
]);

async function reload() {
  await assetsLibraryRef.value?.reload();
}

exposeToChildren({ reload });
</script>
