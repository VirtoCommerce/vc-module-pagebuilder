<template>
  <div class="site-pages">
    <div class="site-pages__bar">
      <label class="site-pages__show">
        <span>{{ $t("PAGE_BUILDER.SITE_TAB.SHOW") }}</span>
        <VcSelect
          v-model="filter"
          class="site-pages__filter"
          :options="filterOptions"
          option-value="value"
          option-label="label"
          :clearable="false"
          @update:model-value="onFilter"
        />
      </label>
    </div>

    <div
      v-if="isStoreContextInvalid"
      class="site-pages__message"
    >
      {{ $t("COMMON.STORE_CONTEXT.INVALID_DESCRIPTION", { storeId }) }}
    </div>

    <div
      v-else
      class="site-pages__list"
    >
      <VcLoading :active="loading" />
      <div
        v-for="page in items"
        :key="page.id"
        class="site-pages__row"
        :class="{ 'site-pages__row--open': page.id === openPageId }"
      >
        <button
          type="button"
          class="site-pages__open"
          @click="openDesigner(page)"
        >
          <span class="site-pages__page">
            <span class="site-pages__name">{{ page.name }}</span>
            <span class="site-pages__permalink">{{ page.permalink }}</span>
          </span>
          <span class="site-pages__status">
            <template v-if="page.page">
              <span
                class="site-pages__dot"
                :class="`site-pages__dot--${pageLifecycle(page.page).toLowerCase()}`"
                aria-hidden="true"
              />
              {{ $t(`PAGE_BUILDER.LIFECYCLE.${pageLifecycle(page.page).toUpperCase()}`) }}
            </template>
            <LanguagePills
              v-else
              :languages="page.languages ?? []"
            />
          </span>
          <span class="site-pages__modified">{{ modified(page.modifiedDate) }}</span>
        </button>
        <button
          type="button"
          class="site-pages__details"
          :title="$t('PAGE_BUILDER.SITE_TAB.DETAILS')"
          :aria-label="$t('PAGE_BUILDER.SITE_TAB.DETAILS')"
          @click="openPage(page)"
        >
          <VcIcon
            icon="lucide-settings-2"
            size="s"
          />
        </button>
      </div>
      <div
        v-if="!loading && !items.length"
        class="site-pages__empty"
      >
        {{ $t("PAGE_BUILDER.SITE_TAB.EMPTY") }}
      </div>
    </div>

    <VcPagination
      v-if="pagination.pages > 1"
      class="site-pages__pagination"
      :pages="pagination.pages"
      :current-page="pagination.currentPage"
      @item-click="pagination.goToPage"
    />
  </div>
</template>

<script lang="ts" setup>
import { computed, onActivated, onMounted, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { formatDateRelative, useBlade, type IBladeToolbar } from "@vc-shell/framework";
import { VcIcon, VcLoading, VcPagination, VcSelect } from "@vc-shell/framework/ui";
import type { PageBuilderPageListItem } from "../../api_client/virtocommerce.pagebuildermodule";
import { canOpenPageDesigner } from "../../utilities/pageDesigner";
import {
  PageLifecycleFilters,
  PageTypes,
  pagesChanged,
  usePageBuilderList,
  usePageDesigner,
  useUrlParams,
} from "../page-builder/composables";
import { pageLifecycle } from "../page-builder/pageLifecycle";
import LanguagePills from "../page-builder/components/languagePills.vue";

const props = defineProps<{
  storeId: string;
  store?: { url?: string; languages?: string[] } | null;
}>();

const ALL = "all";

const { t, locale } = useI18n({ useScope: "global" });
const { openBlade } = useBlade();
const { setStoreId } = useUrlParams();

setStoreId(props.storeId, props.store);
// The host keeps its tabs alive, so coming back to this one restores this site's store.
onActivated(() => setStoreId(props.storeId, props.store));
watch(
  () => props.storeId,
  (storeId) => {
    setStoreId(storeId, props.store);
    reload();
  },
);

const { items, pagination, searchQuery, loadPages, loading, storeContextStatus } = usePageBuilderList({
  pageSize: 20,
  sort: "modifiedDate:desc",
});

const filter = ref<string>(ALL);
const filterOptions = computed(() => [
  { value: ALL, label: t("PAGE_BUILDER.SITE_TAB.FILTER.ALL") },
  { value: PageLifecycleFilters.Draft, label: t("PAGE_BUILDER.SITE_TAB.FILTER.DRAFT") },
  { value: PageLifecycleFilters.Pending, label: t("PAGE_BUILDER.SITE_TAB.FILTER.PENDING") },
  { value: PageLifecycleFilters.Active, label: t("PAGE_BUILDER.SITE_TAB.FILTER.ACTIVE") },
  { value: PageLifecycleFilters.Archived, label: t("PAGE_BUILDER.SITE_TAB.FILTER.ARCHIVED") },
]);
const isStoreContextInvalid = computed(() => ["notFound", "error"].includes(storeContextStatus.value));
const openPageId = ref<string>();

function modified(date?: Date) {
  return date ? formatDateRelative(date, locale.value) : "";
}

async function onFilter() {
  pagination.reset();
  await loadPages({ lifecycle: filter.value === ALL ? undefined : filter.value, skip: 0 });
}

async function reload() {
  await loadPages({ ...searchQuery.value, skip: pagination.skip });
}

// Archived pages have no designer and Multilanguage Pages have one per language, so these open their details.
const designPage = usePageDesigner();
function openDesigner(row: PageBuilderPageListItem) {
  const page = row.page;
  const context = { groupId: page?.id, storeId: props.storeId, cultureName: page?.cultureName, status: page?.status };
  if (!page || !canOpenPageDesigner(context)) return openPage(row);
  openPageId.value = row.id;
  designPage(context, () => {
    openPageId.value = undefined;
    reload();
  });
}

function openPage(row?: PageBuilderPageListItem) {
  const id = row?.id;
  openBlade({
    name: row?.type === PageTypes.Multilanguage ? "MultilanguagePageDetails" : "PageBuilderPageDetails",
    param: id,
    options: { storeId: props.storeId },
    onOpen: () => (openPageId.value = id),
    onClose: () => (openPageId.value = undefined),
  });
}

defineExpose({
  toolbar: computed<IBladeToolbar[]>(() => [
    {
      id: "newPage",
      title: t("PAGE_BUILDER.SITE_TAB.NEW_PAGE"),
      icon: "lucide-plus",
      disabled: storeContextStatus.value !== "ready",
      clickHandler: () => openPage(),
    },
  ]),
});

watch(pagesChanged, reload);
onMounted(reload);
</script>

<style scoped>
.site-pages {
  display: flex;
  flex-direction: column;
  gap: 16px;
  color: var(--neutrals-900);
}

.site-pages__bar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 12px;
}

.site-pages__show {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  color: var(--neutrals-500);
}

.site-pages__filter {
  width: 180px;
}

.site-pages__list {
  position: relative;
  min-height: 64px;
  overflow: hidden;
  border: 1px solid var(--neutrals-200);
  border-radius: 12px;
  background: var(--additional-50);
}

.site-pages__row {
  display: flex;
  align-items: center;
  gap: 4px;
  padding-right: 12px;
}

.site-pages__open {
  display: flex;
  flex: 1 1 auto;
  min-width: 0;
  align-items: center;
  gap: 16px;
  padding: 14px 8px 14px 20px;
  border: 0;
  background: transparent;
  color: inherit;
  font: inherit;
  text-align: left;
  cursor: pointer;
}

.site-pages__details {
  display: flex;
  flex: none;
  align-items: center;
  justify-content: center;
  width: 34px;
  height: 34px;
  border: 0;
  border-radius: 8px;
  background: transparent;
  color: var(--neutrals-500);
  cursor: pointer;
}

.site-pages__details:hover {
  background: var(--neutrals-100);
  color: var(--primary-500);
}

.site-pages__row + .site-pages__row {
  border-top: 1px solid var(--neutrals-200);
}

.site-pages__row:hover {
  background: var(--neutrals-50);
}

.site-pages__open:focus-visible,
.site-pages__details:focus-visible {
  outline: 2px solid var(--primary-500);
  outline-offset: -2px;
}

.site-pages__row--open,
.site-pages__row--open:hover {
  background: var(--primary-50);
}

.site-pages__page {
  display: flex;
  flex: 1 1 auto;
  flex-direction: column;
  min-width: 0;
}

.site-pages__name {
  font-weight: 600;
}

.site-pages__permalink,
.site-pages__status,
.site-pages__modified {
  font-size: 13px;
  color: var(--neutrals-500);
}

.site-pages__name,
.site-pages__permalink {
  overflow: hidden;
  white-space: nowrap;
  text-overflow: ellipsis;
}

.site-pages__status {
  display: inline-flex;
  flex: none;
  align-items: center;
  gap: 7px;
  min-width: 96px;
}

.site-pages__dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--neutrals-400);
}

.site-pages__dot--pending {
  background: var(--info-500);
}

.site-pages__dot--active {
  background: var(--success-500);
}

.site-pages__modified {
  flex: none;
  width: 130px;
  white-space: nowrap;
}

.site-pages__empty,
.site-pages__message {
  padding: 24px 20px;
  font-size: 13px;
  color: var(--neutrals-500);
}

.site-pages__pagination {
  align-self: center;
}

@media (max-width: 640px) {
  .site-pages__modified {
    display: none;
  }
}
</style>
