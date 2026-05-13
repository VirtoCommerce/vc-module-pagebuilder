<template>
  <VcBlade
    width="100%"
    :title="bladeTitle"
    :expanded="expanded"
    :closable="closable"
    :toolbar-items="bladeToolbar"
    @close="$emit('close:blade')"
    @expand="$emit('expand:blade')"
    @collapse="$emit('collapse:blade')"
  >
    <AssetsLibraryContent ref="assetsLibraryRef" />
  </VcBlade>
</template>

<script lang="ts" setup>
import { computed, useTemplateRef } from "vue";
import { useI18n } from "vue-i18n";
import { IBladeToolbar, IParentCallArgs } from "@vc-shell/framework";
import { AssetsLibraryContent, ExposedAssetsLibraryContent } from "../components";

defineOptions({
  name: "AssetsLibrary",
  url: "/page-builder-assets",
  isWorkspace: true,
  permissions: "platform:asset:read",
  menuItem: {
    title: "PAGE_BUILDER.MENU.ASSETS_LIBRARY_TITLE",
    icon: "material-folder",
    priority: 60,
    permissions: "platform:asset:read",
  },
});

interface Props {
  expanded?: boolean;
  closable?: boolean;
  param?: string;
  options?: Record<string, unknown>;
}

interface Emits {
  (event: "parent:call", args: IParentCallArgs): void;
  (event: "close:blade"): void;
  (event: "expand:blade"): void;
  (event: "collapse:blade"): void;
}

withDefaults(defineProps<Props>(), {
  expanded: true,
  closable: true,
});

defineEmits<Emits>();

const { t } = useI18n({ useScope: "global" });

const assetsLibraryRef = useTemplateRef<ExposedAssetsLibraryContent>("assetsLibraryRef");
const bladeTitle = computed(() => t("PAGE_BUILDER.ASSETS.TITLE"));

const bladeToolbar = computed((): IBladeToolbar[] => [
  {
    id: "refresh",
    title: t("PAGE_BUILDER.ASSETS.TOOLBAR.REFRESH"),
    icon: "material-refresh",
    clickHandler: async () => {
      await assetsLibraryRef.value?.reload();
    },
  },
]);

defineExpose({
  title: bladeTitle,
  reload: async () => {
    await assetsLibraryRef.value?.reload();
  },
});
</script>
