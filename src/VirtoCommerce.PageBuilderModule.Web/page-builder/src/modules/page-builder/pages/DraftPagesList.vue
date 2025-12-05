<template>
  <VcBlade
    width="50%"
    :title="bladeTitle"
    :expanded="expanded"
    :closable="closable"
    :toolbar-items="bladeToolbar"
    @close="$emit('close:blade')"
    @expand="$emit('expand:blade')"
    @collapse="$emit('collapse:blade')"
  >
    <PagesList
      ref="pagesListRef"
      :lifecycle="[PageLifecycleFilters.Draft]"
    />
  </VcBlade>
</template>

<script lang="ts" setup>
import { computed, useTemplateRef } from "vue";
import { useI18n } from "vue-i18n";
import { IParentCallArgs } from "@vc-shell/framework";
import { ExposedPagesList, PagesList } from "../components";
import { usePagesListToolbar } from "../composables/usePagesListToolbar";
import { PageLifecycleFilters } from "../composables";
import { GroupedPageBuilderPage } from "src/api_client/virtocommerce.pagebuildermodule";

defineOptions({
  name: "DraftPagesList",
  url: "/page-builder-draft",
  isWorkspace: true,
  menuItem: {
    title: "PAGE_BUILDER.MENU.DRAFT_TITLE",
    icon: "material-article",
    priority: 10,
  },
});

interface Props {
  expanded?: boolean;
  closable?: boolean;
  param?: string;
  options?: Record<string, unknown>;
}

interface Emits {
  (event: "parent:call", args: IParentCallArgs): void;
  (event: "close:blade"): void;
  (event: "expand:blade"): void;
  (event: "collapse:blade"): void;
}

withDefaults(defineProps<Props>(), {
  expanded: true,
  closable: true,
});

defineEmits<Emits>();

const { t } = useI18n({ useScope: "global" });

const pagesListRef = useTemplateRef<ExposedPagesList>("pagesListRef");
const bladeTitle = computed(() => t("PAGE_BUILDER.PAGES.LIST.DRAFT_TITLE"));

const bladeToolbar = usePagesListToolbar(PageLifecycleFilters.Draft, pagesListRef);

async function reload() {
  pagesListRef.value?.reload();
}

defineExpose({
  title: bladeTitle,
  reload,
  onItemClick: (x: GroupedPageBuilderPage) => pagesListRef.value?.onItemClick?.(x),
});
</script>
