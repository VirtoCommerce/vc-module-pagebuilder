<template>
  <VcBlade
    width="50%"
    :title="bladeTitle"
    :toolbar-items="bladeToolbar"
  >
    <PagesList
      ref="pagesListRef"
      :lifecycle="[PageLifecycleFilters.Archived]"
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
  name: "ArchivedPagesList",
  url: "/page-builder-archived",
  isWorkspace: true,
  menuItem: {
    title: "PAGE_BUILDER.MENU.ARCHIVED_TITLE",
    icon: "lucide-file-text",
    priority: 40,
  },
});

const { t } = useI18n({ useScope: "global" });

const pagesListRef = useTemplateRef<ExposedPagesList>("pagesListRef");
const bladeTitle = computed(() => t("PAGE_BUILDER.PAGES.LIST.ARCHIVED_TITLE"));

const bladeToolbar = usePagesListToolbar(PageLifecycleFilters.Archived, pagesListRef);
</script>
