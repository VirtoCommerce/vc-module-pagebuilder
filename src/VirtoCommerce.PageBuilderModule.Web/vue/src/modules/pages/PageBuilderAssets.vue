<template>
  <AssetsLibraryBlade />
</template>

<script lang="ts" setup>
import { useBlade } from "@vc-shell/framework";
import AssetsLibraryBlade from "../asset-library/components/AssetsLibraryBlade.vue";
import { useUrlParams } from "../page-builder/composables";

// a store's Assets Library (param = store id); the standalone app has it as the AssetsLibrary workspace
defineBlade({
  name: "PageBuilderAssets",
  url: "/page-builder-assets",
  permissions: "platform:asset:read",
});

const { param } = useBlade();
if (param.value) {
  useUrlParams().setStoreId(param.value);
}
</script>
