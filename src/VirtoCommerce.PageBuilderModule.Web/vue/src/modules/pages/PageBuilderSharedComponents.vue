<template>
  <SharedComponentsBlade />
</template>

<script lang="ts" setup>
import { useBlade } from "@vc-shell/framework";
import SharedComponentsBlade from "../shared-components/components/SharedComponentsBlade.vue";
import { useUrlParams } from "../page-builder/composables";

defineBlade({
  name: "PageBuilderSharedComponents",
  url: "/page-builder-shared-components",
  permissions: "builder:shared-components:read",
});

const { param } = useBlade();
if (param.value) {
  useUrlParams().setStoreId(param.value);
}
</script>
