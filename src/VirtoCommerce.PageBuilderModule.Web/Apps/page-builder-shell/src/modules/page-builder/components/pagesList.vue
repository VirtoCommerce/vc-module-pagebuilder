<template>
  <div
    v-if="isStoreContextInvalid"
    class="pages-list__store-context"
  >
    <VcIcon
      icon="lucide-triangle-alert"
      class="pages-list__store-context-icon"
    />
    <div class="pages-list__store-context-title">
      {{ storeContextTitle }}
    </div>
    <VcHint class="pages-list__store-context-text">
      {{ storeContextDescription }}
    </VcHint>
  </div>

  <VcDataTable
    v-else
    class="tw-grow tw-basis-0"
    :items="items"
    :loading="loading"
    :total-count="pagination.totalCount"
    :pagination="pagination"
    :searchable="true"
    :selection-mode="'multiple'"
    :global-filters="computedGlobalFilters"
    :row-actions="actionBuilder"
    v-model:active-item-id="selectedItemId"
    v-model:sort-field="sortField"
    v-model:sort-order="sortOrder"
    v-model:selection="localSelection"
    @row-click="onItemClick"
    @search="onSearchList"
    @pagination-click="pagination.goToPage"
    @filter="onFilter"
  >
    <VcColumn
      id="name"
      :title="$t('PAGE_BUILDER.PAGES.LIST.TABLE.HEADER.NAME')"
      :always-visible="true"
      :sortable="true"
      field="name"
    />
    <VcColumn
      id="type"
      :title="$t('PAGE_BUILDER.PAGES.LIST.TABLE.HEADER.TYPE')"
      :always-visible="true"
      :sortable="false"
      field="type"
    >
      <template #body="{ data }">
        <Tag :text="$t(`PAGE_BUILDER.PAGE_TYPES.${(data.type ?? PageTypes.Landing).toUpperCase()}`)" />
      </template>
    </VcColumn>
    <VcColumn
      id="languages"
      :title="$t('PAGE_BUILDER.PAGES.LIST.TABLE.HEADER.LANGUAGES')"
      :always-visible="true"
      :sortable="false"
      field="languages"
    >
      <template #body="{ data }">
        <LanguagePills :languages="rowLanguages(data)" />
      </template>
    </VcColumn>
    <VcColumn
      id="permalink"
      :title="$t('PAGE_BUILDER.PAGES.LIST.TABLE.HEADER.PERMALINK')"
      :always-visible="true"
      :sortable="true"
      field="permalink"
    />
    <VcColumn
      id="modifiedDate"
      :title="$t('PAGE_BUILDER.PAGES.LIST.TABLE.HEADER.MODIFIED_DATE')"
      :always-visible="true"
      :sortable="true"
      type="datetime"
      field="modifiedDate"
    />
    <VcColumn
      id="modifiedBy"
      :title="$t('PAGE_BUILDER.PAGES.LIST.TABLE.HEADER.MODIFIED_BY')"
      :sortable="false"
      field="modifiedBy"
    >
      <template #body="{ data }">
        {{ data.page?.modifiedBy ?? data.multilanguagePage?.modifiedBy }}
      </template>
    </VcColumn>
    <VcColumn
      id="status"
      :title="$t('PAGE_BUILDER.PAGES.LIST.TABLE.HEADER.STATUS')"
      :sortable="false"
      field="status"
    >
      <template #body="{ data }">
        <PageStatus
          v-if="data.page"
          extended
          :item="data.page"
        />
        <span
          v-else-if="data.languages"
          class="tw-text-sm"
        >
          {{ multilanguageStatusText(data.languages) }}
        </span>
      </template>
    </VcColumn>
  </VcDataTable>
  <AddPagePopup
    v-if="isAddPopupOpen"
    @close="isAddPopupOpen = false"
    @select="onAddTypeSelected"
  />
  <input
    ref="fileInputRef"
    type="file"
    accept=".json"
    style="display: none"
    @change="onFileSelected"
  />
</template>
<script lang="ts" setup>
import { ref, Ref, computed, watch, onMounted, readonly } from "vue";
import { useI18n } from "vue-i18n";
import { debounce } from "lodash-es";
import { useBlade, usePopup, useDataTableSort, IActionBuilderResult, notification } from "@vc-shell/framework";
import {
  GroupedPageBuilderPage,
  MultilanguagePageLanguage,
  PageBuilderPageListItem,
} from "../../../api_client/virtocommerce.pagebuildermodule";
import {
  PageLifecycleFilters,
  PageTypes,
  TranslationStatuses,
  usePageBuilderList,
  useUrlParams,
  useAiAgentContextWithStore,
  refreshMenuBadges,
} from "../composables";
import { parseImportFile } from "../composables/usePageContentApi";
import {
  hasLanguageVersions,
  isMultilanguageRowArchived,
  summarizeLanguageStatuses,
} from "../composables/useMultilanguagePageDetails/helpers";
import PageStatus from "./pageStatus.vue";
import LanguagePills from "./languagePills.vue";
import AddPagePopup from "./addPagePopup.vue";
import Tag from "./tag.vue";

import { VcColumn, VcDataTable, VcHint, VcIcon } from "@vc-shell/framework/ui";

interface Props {
  lifecycle?: PageLifecycleFilters[];
}

const props = defineProps<Props>();

const { t } = useI18n({ useScope: "global" });
const { param, openBlade } = useBlade();
const { showConfirmation } = usePopup();
const { storeId, initUrlParams, getLanguages } = useUrlParams();

const { sortField, sortOrder, sortExpression } = useDataTableSort({
  initialDirection: "DESC",
  initialField: "modifiedDate",
});

const { items, pagination, searchQuery, loadPages, removePages, loading, pageStatuses, storeContextStatus } = usePageBuilderList({
  pageSize: 20,
  sort: sortExpression.value,
  lifecycle: props.lifecycle,
});

const selectedItemId = ref<string>();
const localSelection = ref<PageBuilderPageListItem[]>([]);
const isAddPopupOpen = ref(false);
const selectedItems = computed<string[]>(() =>
  localSelection.value.map((item) => item.id!).filter((id): id is string => !!id),
);
const fileInputRef = ref<HTMLInputElement | null>(null);
const searchValue = ref<string>();
const isStoreContextInvalid = computed(() => ["missing", "notFound", "error"].includes(storeContextStatus.value));
const storeContextTitle = computed(() =>
  storeContextStatus.value === "missing"
    ? t("COMMON.STORE_CONTEXT.MISSING_TITLE")
    : t("COMMON.STORE_CONTEXT.INVALID_TITLE"),
);
const storeContextDescription = computed(() =>
  storeContextStatus.value === "missing"
    ? t("COMMON.STORE_CONTEXT.MISSING_DESCRIPTION")
    : t("COMMON.STORE_CONTEXT.INVALID_DESCRIPTION", { storeId: storeId.value }),
);

const storeLanguages = ref<string[]>([]);

const computedGlobalFilters = computed(() => [
  {
    id: "statuses",
    label: t("PAGE_BUILDER.PAGES.LIST.TABLE.FILTER.STATUS"),
    filter: {
      options: pageStatuses.value.map((s) => ({
        value: s.value,
        label: s.label,
      })),
      multiple: false,
    },
  },
  {
    id: "pageType",
    label: t("PAGE_BUILDER.PAGES.LIST.TABLE.FILTER.PAGE_TYPE"),
    filter: {
      options: Object.values(PageTypes).map((value) => ({
        value,
        label: t(`PAGE_BUILDER.PAGE_TYPES.${value.toUpperCase()}`),
      })),
      multiple: false,
    },
  },
  {
    id: "languageCode",
    label: t("PAGE_BUILDER.PAGES.LIST.TABLE.FILTER.LANGUAGE"),
    filter: {
      options: storeLanguages.value.map((value) => ({ value, label: value })),
      multiple: false,
    },
  },
  {
    id: "translationStatus",
    label: t("PAGE_BUILDER.PAGES.LIST.TABLE.FILTER.TRANSLATION_STATUS"),
    filter: {
      options: Object.values(TranslationStatuses).map((value) => ({
        value,
        label: t(`PAGE_BUILDER.STATUS.${value.toUpperCase()}`),
      })),
      multiple: false,
    },
  },
]);

/** A list row, or a page reported back by a details blade. */
export type PagesListClickItem = PageBuilderPageListItem | GroupedPageBuilderPage;

function onItemClick(event: { data: PagesListClickItem }) {
  const item = event.data;
  const isMultilanguage = "type" in item && item.type === PageTypes.Multilanguage;
  openBlade({
    name: isMultilanguage ? "MultilanguagePageDetails" : "PageDetails",
    param: item.id,
    options: {
      storeId: storeId.value ?? undefined,
    },
    onOpen() {
      selectedItemId.value = item.id;
    },
    onClose() {
      selectedItemId.value = undefined;
    },
  });
}

const onSearchList = debounce(async (keyword: string | undefined) => {
  searchValue.value = keyword;
  await loadPages({
    ...searchQuery.value,
    keyword,
  });
}, 1000);

// A Landing Page row shows its own language and status.
function rowLanguages(item: PageBuilderPageListItem): MultilanguagePageLanguage[] {
  if (item.languages) {
    return item.languages;
  }

  return item.page
    ? [{ cultureName: item.page.cultureName, status: item.page.status, hasChanges: item.page.hasChanges }]
    : [];
}

// Each language has its own status in the pills; the row sums them up.
function multilanguageStatusText(languages: MultilanguagePageLanguage[]): string {
  const summary = summarizeLanguageStatuses(languages);
  const published = t("PAGE_BUILDER.MULTILANGUAGE.LIST.PUBLISHED_COUNT", summary);
  return summary.outdated ? `${published}, ${t("PAGE_BUILDER.MULTILANGUAGE.LIST.OUTDATED_COUNT", summary)}` : published;
}

function isArchived(item: PageBuilderPageListItem): boolean {
  return item.type === PageTypes.Multilanguage
    ? isMultilanguageRowArchived(item.languages)
    : item.page?.status === "Archived";
}

const actionBuilder = (item: PageBuilderPageListItem) => {
  const result: IActionBuilderResult[] = [];

  if (!isArchived(item)) {
    // A Multilanguage Page without versions has nothing to archive and is deleted.
    const isEmptyMultilanguage = item.type === PageTypes.Multilanguage && !hasLanguageVersions(item.languages);
    result.push({
      icon: "lucide-trash-2",
      title: isEmptyMultilanguage
        ? t("PAGE_BUILDER.MULTILANGUAGE.TOOLBAR.DELETE")
        : t("PAGE_BUILDER.PAGES.LIST.TABLE.ACTIONS.DELETE"),
      type: "danger",
      clickHandler: async () => {
        const confirmation = isEmptyMultilanguage
          ? t("PAGE_BUILDER.MULTILANGUAGE.ALERTS.DELETE")
          : t("PAGE_BUILDER.PAGES.ALERTS.DELETE");
        if (item.id && (await showConfirmation(confirmation))) {
          await removePages({ items: [item] });
          await reload();
        }
      },
    });
  }

  return result;
};

watch(
  () => sortExpression.value,
  async (newVal) => {
    await loadPages({
      ...searchQuery.value,
      sort: newVal,
    });
  },
);

watch(
  () => param.value,
  (newVal) => {
    selectedItemId.value = newVal as string | undefined;
  },
  { immediate: true },
);

function filterValue(value: unknown): string | undefined {
  const joined = Array.isArray(value) ? value.join(",") : (value as string | undefined);
  return joined || undefined;
}

async function onFilter(event: { filters: Record<string, unknown> }) {
  pagination.reset();
  await loadPages({
    ...searchQuery.value,
    statuses: filterValue(event.filters.statuses),
    pageType: filterValue(event.filters.pageType),
    languageCode: filterValue(event.filters.languageCode),
    translationStatus: filterValue(event.filters.translationStatus),
    skip: 0,
  });
}

function openLoadFlow() {
  fileInputRef.value?.click();
}

async function onFileSelected(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  if (!file) return;
  input.value = "";

  const importData = await parseImportFile(file);
  notification.success(t("PAGE_BUILDER.PAGES.ALERTS.LOAD_CONTENT_SUCCESS"));

  openBlade({
    name: "PageDetails",
    options: {
      storeId: storeId.value ?? undefined,
      importData,
    },
  });
}

async function openAddBlade() {
  // A Multilanguage Page needs more than one store language.
  if ((await getLanguages()).length > 1) {
    isAddPopupOpen.value = true;
  } else {
    onAddTypeSelected(PageTypes.Landing);
  }
}

function onAddTypeSelected(type: PageTypes) {
  isAddPopupOpen.value = false;
  openBlade({
    name: type === PageTypes.Multilanguage ? "MultilanguagePageDetails" : "PageDetails",
    options: {
      storeId: storeId.value ?? undefined,
    },
  });
}

async function reload() {
  await loadPages({
    ...searchQuery.value,
    skip: pagination.skip,
    sort: sortExpression.value,
  });
  localSelection.value = [];
  refreshMenuBadges();
}

async function removeSelectedPages() {
  if (
    await showConfirmation(
      t("PAGE_BUILDER.PAGES.ALERTS.DELETE_SELECTED_CONFIRMATION.MESSAGE", {
        count: localSelection.value.length,
      }),
    )
  ) {
    await removePages({ items: localSelection.value });
    await reload();
  }
}

onMounted(async () => {
  initUrlParams();
  await loadPages();
  storeLanguages.value = await getLanguages();
});

export interface ExposedPagesList {
  selectedItems: Readonly<Ref<readonly string[]>>;
  reload: () => Promise<void>;
  removeSelectedPages: () => Promise<void>;
  onItemClick: (event: { data: PagesListClickItem }) => void;
  openAddBlade: () => Promise<void>;
  openLoadFlow: () => void;
}

defineExpose<ExposedPagesList>({
  selectedItems: readonly(selectedItems) as unknown as Readonly<Ref<readonly string[]>>,
  reload,
  removeSelectedPages,
  onItemClick,
  openAddBlade,
  openLoadFlow,
});
</script>

<style lang="scss" scoped>
.pages-list {
  &__store-context {
    @apply tw-flex tw-h-full tw-flex-1 tw-flex-col tw-items-center tw-justify-center tw-gap-3 tw-bg-[color:var(--neutrals-50)] tw-p-8 tw-text-center;
  }

  &__store-context-icon {
    @apply tw-text-[64px] tw-text-[color:var(--danger-500)];
  }

  &__store-context-title {
    @apply tw-text-lg tw-font-semibold tw-text-[color:var(--neutrals-800)];
  }

  &__store-context-text {
    @apply tw-max-w-[460px];
  }
}
</style>
