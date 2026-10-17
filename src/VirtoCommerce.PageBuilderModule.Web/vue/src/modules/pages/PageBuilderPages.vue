<template>
  <VcBlade
    width="70%"
    :title="bladeTitle"
    :toolbar-items="bladeToolbar"
  >
    <div class="page-builder-pages">
      <VcButtonGroup
        class="page-builder-pages__statuses"
        attached
        size="sm"
        role="group"
        :aria-label="$t('PAGE_BUILDER.PAGES.LIST.TABLE.HEADER.STATUS')"
      >
        <VcButton
          v-for="option in statusOptions"
          :key="option.id"
          variant="secondary"
          :selected="status === option.id"
          :aria-pressed="status === option.id"
          @click="status = option.id"
        >
          {{ option.title }}
          <span
            v-if="option.count"
            class="page-builder-pages__count"
            >{{ option.count }}</span
          >
        </VcButton>
      </VcButtonGroup>

      <PagesList
        :key="status"
        ref="pagesListRef"
        :lifecycle="status === ALL ? undefined : [status]"
      />
    </div>
  </VcBlade>
</template>

<script lang="ts" setup>
import { computed, onMounted, ref, useTemplateRef } from "vue";
import { useI18n } from "vue-i18n";
import { useBlade, type IBladeToolbar } from "@vc-shell/framework";
import { VcBlade, VcButton, VcButtonGroup } from "@vc-shell/framework/ui";
import { PagesList, type ExposedPagesList } from "../page-builder/components";
import { PageLifecycleFilters, pageCounts, refreshMenuBadges, useUrlParams } from "../page-builder/composables";
import { usePagesListToolbar } from "../page-builder/composables/usePagesListToolbar";

// A store's pages (param = store id), opened from the store blade's Page Builder widget: what the standalone app shows
// as one workspace per status, here one blade with a status switch.
defineBlade({
  name: "PageBuilderPages",
  url: "/page-builder-pages",
});

const ALL = "all";
type Status = PageLifecycleFilters | typeof ALL;

const { t } = useI18n({ useScope: "global" });
const { param, openBlade } = useBlade();
const { setStoreId } = useUrlParams();
if (param.value) {
  setStoreId(param.value);
}

const status = ref<Status>(ALL);
const pagesListRef = useTemplateRef<ExposedPagesList>("pagesListRef");

const statusOptions = computed((): { id: Status; title: string; count?: number }[] => [
  { id: PageLifecycleFilters.Draft, title: t("PAGE_BUILDER.MENU.DRAFT_TITLE"), count: pageCounts.draft.value },
  { id: PageLifecycleFilters.Pending, title: t("PAGE_BUILDER.MENU.PENDING_TITLE"), count: pageCounts.pending.value },
  { id: PageLifecycleFilters.Active, title: t("PAGE_BUILDER.MENU.ACTIVE_TITLE"), count: pageCounts.active.value },
  { id: PageLifecycleFilters.Archived, title: t("PAGE_BUILDER.MENU.ARCHIVED_TITLE"), count: pageCounts.archived.value },
  { id: ALL, title: t("PAGE_BUILDER.MENU.TITLE"), count: pageCounts.all.value },
]);

const bladeTitle = computed(() => t("PAGE_BUILDER.PAGES.LIST.STORE_TITLE", { store: param.value }));

const listToolbar = usePagesListToolbar(() => (status.value === ALL ? null : status.value), pagesListRef);
const bladeToolbar = computed((): IBladeToolbar[] => [
  ...listToolbar.value,
  {
    id: "assets",
    icon: "lucide-folder",
    title: t("PAGE_BUILDER.PAGES.LIST.TOOLBAR.ASSETS"),
    permissions: "platform:asset:read",
    clickHandler: () => openBlade({ name: "PageBuilderAssets", param: param.value }),
  },
  {
    id: "sharedComponents",
    icon: "lucide-blocks",
    title: t("PAGE_BUILDER.PAGES.LIST.TOOLBAR.SHARED_COMPONENTS"),
    permissions: "builder:shared-components:read",
    clickHandler: () => openBlade({ name: "PageBuilderSharedComponents", param: param.value }),
  },
]);

onMounted(refreshMenuBadges);
</script>

<style scoped>
.page-builder-pages {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
}

.page-builder-pages__statuses {
  flex: none;
  align-self: flex-start;
  margin: 12px 16px;
}

.page-builder-pages__count {
  margin-left: 6px;
  color: var(--neutrals-500);
  font-variant-numeric: tabular-nums;
}
</style>
