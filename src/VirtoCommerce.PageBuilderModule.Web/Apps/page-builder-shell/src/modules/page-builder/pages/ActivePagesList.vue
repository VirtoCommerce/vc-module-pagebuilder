<template>
  <VcBlade
           width="50%"
           :title="bladeTitle"
           :toolbar-items="bladeToolbar">
    <PagesList
               ref="pagesListRef"
               :lifecycle="[PageLifecycleFilters.Active]" />
  </VcBlade>
</template>

<script lang="ts" setup>
import { computed, useTemplateRef } from "vue";
import { useI18n } from "vue-i18n";
import { ExposedPagesList, PagesList } from "../components";
import { usePagesListToolbar } from "../composables/usePagesListToolbar";
import { PageLifecycleFilters } from "../composables";
import { GroupedPageBuilderPage } from "../../../api_client/virtocommerce.pagebuildermodule";

import { VcBlade } from "@vc-shell/framework/ui";

import { useBlade } from "@vc-shell/framework";

const { exposeToChildren } = useBlade();

defineBlade({
  name: "ActivePagesList",
  url: "/page-builder-active",
  isWorkspace: true,
  menuItem: {
    title: "PAGE_BUILDER.MENU.ACTIVE_TITLE",
    icon: "lucide-file-text",
    priority: 30,
  },
});

const { t } = useI18n({ useScope: "global" });

const pagesListRef = useTemplateRef<ExposedPagesList>("pagesListRef");
const bladeTitle = computed(() => t("PAGE_BUILDER.PAGES.LIST.ACTIVE_TITLE"));

// here should be additional filter by dates
const bladeToolbar = usePagesListToolbar(PageLifecycleFilters.Active, pagesListRef);

async function reload() {
  await pagesListRef.value?.reload();
}

exposeToChildren({
  reload,
  onItemClick: (x: GroupedPageBuilderPage) => pagesListRef.value?.onItemClick?.({ data: x }),
});
</script>
