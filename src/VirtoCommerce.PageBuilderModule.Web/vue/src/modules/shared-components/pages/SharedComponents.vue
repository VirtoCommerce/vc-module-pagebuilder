<template>
  <SharedComponentsBlade />
</template>

<script lang="ts" setup>
import SharedComponentsBlade from "../components/SharedComponentsBlade.vue";

// the standalone app's workspace; the Admin Shell UI opens the same body as PageBuilderSharedComponents
defineBlade({
  name: "SharedComponents",
  url: "/page-builder-shared-components",
  isWorkspace: true,
  permissions: "builder:shared-components:read",
  menuItem: {
    title: "SHARED_COMPONENTS.MENU.TITLE",
    icon: "lucide-blocks",
    priority: 61,
    permissions: "builder:shared-components:read",
  },
});
</script>
