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
import { injectBladeContext } from "@vc-shell/framework";
import { VcWidget } from "@vc-shell/framework/ui";

// the store shared by the Store module's blade via defineBladeContext({ item })
const ctx = injectBladeContext();
const storeId = computed(() => (ctx.value.item as { id?: string } | undefined)?.id);

// the Page Builder is this module's own app (/apps/page-builder-shell); it reads the store from the query string.
// The host uses hash routing, so the pathname is the deployment path: keep its prefix (e.g. /platform/), as the content module does
function open() {
  const prefix = window.location.pathname.replace(/\/apps\/.*$/, "").replace(/\/$/, "");
  window.open(`${prefix}/apps/page-builder-shell/?storeId=${encodeURIComponent(storeId.value!)}`, "_blank");
}
</script>
