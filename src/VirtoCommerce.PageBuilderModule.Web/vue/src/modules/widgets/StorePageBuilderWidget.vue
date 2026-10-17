<template>
  <VcWidget
    icon="lucide-layout-template"
    :title="$t('PAGE_BUILDER.WIDGETS.STORE')"
    :disabled="!storeId"
    @click="open"
  />
</template>

<script setup lang="ts">
import { computed } from "vue";
import { injectBladeContext, useBlade } from "@vc-shell/framework";
import { VcWidget } from "@vc-shell/framework/ui";

// the store shared by the Store module's blade via defineBladeContext({ item })
const ctx = injectBladeContext();
const storeId = computed(() => (ctx.value.item as { id?: string } | undefined)?.id);

// the store's pages, inline next to the store (the AngularJS admin's widget still opens the standalone app in a new tab)
const { openBlade } = useBlade();
function open() {
  if (storeId.value) openBlade({ name: "PageBuilderPages", param: storeId.value });
}
</script>
