<template>
  <VcBlade
    width="50%"
    :title="bladeTitle"
    :toolbar-items="bladeToolbar"
  >
    <PagesList
      ref="pagesListRef"
      :param="param"
    />
  </VcBlade>
</template>

<script lang="ts" setup>
import { computed, useTemplateRef } from "vue";
import { useI18n } from "vue-i18n";
import { ExposedPagesList, PagesList } from "../components";
import { usePagesListToolbar } from "../composables/usePagesListToolbar";
import { GroupedPageBuilderPage } from "src/api_client/virtocommerce.pagebuildermodule";

import { VcBlade } from "@vc-shell/framework/ui";

import { useBlade } from "@vc-shell/framework";

const { exposeToChildren, param } = useBlade();

defineBlade({
  name: "AllPagesList",
  url: "/page-builder",
  isWorkspace: true,
  menuItem: {
    title: "PAGE_BUILDER.MENU.TITLE",
    icon: "lucide-file-text",
    priority: 50,
  },
});

const { t } = useI18n({ useScope: "global" });

const pagesListRef = useTemplateRef<ExposedPagesList>("pagesListRef");
const bladeTitle = computed(() => t("PAGE_BUILDER.PAGES.LIST.TITLE"));

const bladeToolbar = usePagesListToolbar(null, pagesListRef);

async function reload() {
  await pagesListRef.value?.reload();
}

exposeToChildren({
  reload,
  onItemClick: (x: GroupedPageBuilderPage) => pagesListRef.value?.onItemClick?.({ data: x }),
});
</script>
