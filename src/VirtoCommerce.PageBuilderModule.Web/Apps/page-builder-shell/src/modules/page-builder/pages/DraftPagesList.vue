<template>
  <VcBlade
    width="50%"
    :title="bladeTitle"
    :toolbar-items="bladeToolbar"
  >
    <PagesList
      ref="pagesListRef"
      :param="param"
      :lifecycle="[PageLifecycleFilters.Draft]"
    />
  </VcBlade>
</template>

<script lang="ts" setup>
import { computed, useTemplateRef } from "vue";
import { useI18n } from "vue-i18n";
import { ExposedPagesList, PagesList } from "../components";
import { usePagesListToolbar } from "../composables/usePagesListToolbar";
import { PageLifecycleFilters } from "../composables";
import { GroupedPageBuilderPage } from "src/api_client/virtocommerce.pagebuildermodule";

import { VcBlade } from "@vc-shell/framework/ui";

import { useBlade } from "@vc-shell/framework";

const { exposeToChildren, param } = useBlade();

defineBlade({
  name: "DraftPagesList",
  url: "/page-builder-draft",
  isWorkspace: true,
  menuItem: {
    title: "PAGE_BUILDER.MENU.DRAFT_TITLE",
    icon: "lucide-file-text",
    priority: 10,
  },
});

const { t } = useI18n({ useScope: "global" });

const pagesListRef = useTemplateRef<ExposedPagesList>("pagesListRef");
const bladeTitle = computed(() => t("PAGE_BUILDER.PAGES.LIST.DRAFT_TITLE"));

const bladeToolbar = usePagesListToolbar(PageLifecycleFilters.Draft, pagesListRef);

async function reload() {
  await pagesListRef.value?.reload();
}

exposeToChildren({
  reload,
  onItemClick: (x: GroupedPageBuilderPage) => pagesListRef.value?.onItemClick?.({ data: x }),
});
</script>
