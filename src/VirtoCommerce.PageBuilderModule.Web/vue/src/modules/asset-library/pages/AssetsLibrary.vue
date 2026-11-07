<template>
  <AssetsLibraryBlade />
</template>

<script lang="ts" setup>
import AssetsLibraryBlade from "../components/AssetsLibraryBlade.vue";

// the standalone app's workspace; the Admin Shell UI opens the same body as PageBuilderAssets
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
</script>
