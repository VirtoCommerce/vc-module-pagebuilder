<template>
  <VcBlade
    width="50%"
    :title="bladeTitle"
    :toolbar-items="bladeToolbar"
  >
    <PagesList
      ref="pagesListRef"
      :lifecycle="[PageLifecycleFilters.Active]"
    />
  </VcBlade>
</template>

<script lang="ts" setup>
import { computed, useTemplateRef } from "vue";
import { useI18n } from "vue-i18n";
import { ExposedPagesList, PagesList } from "../components";
import { usePagesListToolbar } from "../composables/usePagesListToolbar";
import { PageLifecycleFilters } from "../composables";

import { VcBlade } from "@vc-shell/framework/ui";

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

const bladeToolbar = usePagesListToolbar(PageLifecycleFilters.Active, pagesListRef);
</script>
