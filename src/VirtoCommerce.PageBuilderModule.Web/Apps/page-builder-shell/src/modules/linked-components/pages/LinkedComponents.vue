<template>
  <VcBlade
    width="100%"
    :title="bladeTitle"
    :toolbar-items="bladeToolbar"
  >
    <LinkedComponentsContent ref="linkedComponentsRef" />
  </VcBlade>
</template>

<script lang="ts" setup>
import { computed, useTemplateRef } from "vue";
import { useI18n } from "vue-i18n";
import { type IBladeToolbar, useBlade } from "@vc-shell/framework";
import { VcBlade } from "@vc-shell/framework/ui";
import { LinkedComponentsContent, type ExposedLinkedComponentsContent } from "../components";

defineBlade({
  name: "LinkedComponents",
  url: "/page-builder-linked-components",
  isWorkspace: true,
  permissions: "builder:linked-components:read",
  menuItem: {
    title: "LINKED_COMPONENTS.MENU.TITLE",
    icon: "lucide-blocks",
    priority: 61,
    permissions: "builder:linked-components:read",
  },
});

const { exposeToChildren } = useBlade();
const { t } = useI18n({ useScope: "global" });
const linkedComponentsRef = useTemplateRef<ExposedLinkedComponentsContent>("linkedComponentsRef");
const bladeTitle = computed(() => t("LINKED_COMPONENTS.TITLE"));
const bladeToolbar = computed((): IBladeToolbar[] => [
  {
    id: "refresh",
    title: t("LINKED_COMPONENTS.TOOLBAR.REFRESH"),
    icon: "lucide-refresh-cw",
    clickHandler: async () => {
      await reload();
    },
  },
]);

async function reload() {
  await linkedComponentsRef.value?.reload();
}

exposeToChildren({ reload });
</script>
