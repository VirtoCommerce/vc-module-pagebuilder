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
import { AssetsLibraryContent, ExposedAssetsLibraryContent } from "../components";

defineBlade({
  name: "AssetsLibrary",
  url: "/page-builder-assets",
  isWorkspace: true,
  permissions: "platform:asset:read",
  menuItem: {
    title: "ASSET_LIBRARY.MENU.TITLE",
    icon: "lucide-folder",
    priority: 60,
    permissions: "platform:asset:read",
  },
});

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
