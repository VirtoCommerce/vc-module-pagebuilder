<template>
  <VcBlade
    width="100%"
    :title="bladeTitle"
    :toolbar-items="bladeToolbar"
  >
    <div
      class="tw-flex tw-h-full tw-flex-col tw-bg-[color:var(--neutrals-50)] tw-text-sm tw-text-[color:var(--neutrals-800)]"
    >
      <div
        v-if="isStoreContextInvalid"
        class="tw-flex tw-h-full tw-flex-1 tw-flex-col tw-items-center tw-justify-center tw-gap-3 tw-p-8 tw-text-center"
      >
        <VcIcon
          icon="lucide-triangle-alert"
          size="xxxl"
          variant="danger"
        />
        <div class="tw-text-lg tw-font-semibold">
          {{ storeContextTitle }}
        </div>
        <VcHint class="tw-max-w-md">
          {{ storeContextDescription }}
        </VcHint>
      </div>

      <div
        v-else
        class="tw-flex tw-min-h-0 tw-flex-1 tw-flex-col lg:tw-flex-row"
      >
        <section
          class="tw-flex tw-min-h-0 tw-grow tw-basis-0 tw-flex-col"
          @click.self="clearSelection"
        >
          <div
            v-if="loadError"
            class="tw-m-4 tw-flex tw-items-start tw-gap-3 tw-rounded-md tw-border tw-border-solid tw-border-[color:var(--danger-200)] tw-bg-[color:var(--danger-50)] tw-p-4 tw-text-[color:var(--danger-700)]"
            role="alert"
          >
            <VcIcon
              icon="lucide-circle-alert"
              size="l"
              variant="danger"
            />
            <div class="tw-min-w-0 tw-flex-1">
              <div class="tw-font-semibold">
                {{ $t("SHARED_COMPONENTS.NOTIFICATIONS.ERROR_GENERIC") }}
              </div>
              <div class="tw-mt-1 tw-break-words">
                {{ loadError }}
              </div>
            </div>
            <VcButton
              variant="secondary"
              icon="lucide-refresh-cw"
              :disabled="contentLoading"
              @click="handleRetry"
            >
              {{ $t("SHARED_COMPONENTS.TOOLBAR.REFRESH") }}
            </VcButton>
          </div>

          <SharedComponentsTable
            v-if="!loadError || items.length > 0"
            :components="items"
            :total-count="totalCount"
            :pagination="pagination"
            :selected-component-id="selectedComponent?.id"
            :search-value="searchValue"
            :can-update="canUpdate"
            :can-delete="canDelete"
            :loading="contentLoading"
            @select="handleSelect"
            @search="handleSearch"
            @page-change="pagination.goToPage"
            @rename="openRenamePopup"
            @delete="handleDelete"
          />
        </section>

        <SharedComponentDetails
          v-if="selectedComponent"
          :component="selectedComponent"
          :can-update="canUpdate"
          :can-delete="canDelete"
          :can-open-designer="canOpenDesigner"
          :loading="loading"
          :details-loading="detailsLoading"
          @close="clearSelection"
          @rename="openRenamePopup"
          @delete="handleDelete"
          @open-designer="openUsagePageDesigner"
        />
      </div>
    </div>
  </VcBlade>

  <RenameSharedComponentPopup
    v-if="renameTarget"
    :component="renameTarget"
    :submitting="loading"
    :server-error="renameError"
    @clear-error="clearRenameError"
    @close="closeRenamePopup"
    @rename="handleRename"
  />
</template>

<script lang="ts" setup>
import { computed, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { type IBladeToolbar, useBlade, usePermissions } from "@vc-shell/framework";
import { VcBlade, VcButton, VcHint, VcIcon } from "@vc-shell/framework/ui";
import { SharedComponentDetails, SharedComponentsTable, RenameSharedComponentPopup } from "../components";
import { useSharedComponentActions, useSharedComponents } from "../composables";
import type { SharedComponent, SharedComponentUsagePage } from "../types";
import { canOpenPageDesigner, openPageDesigner } from "../../../utilities/pageDesigner";

defineBlade({
  name: "SharedComponents",
  url: "/page-builder-shared-components",
  isWorkspace: true,
  permissions: "builder:shared-components:read",
  menuItem: {
    title: "SHARED_COMPONENTS.MENU.TITLE",
    icon: "lucide-blocks",
    priority: 61,
    permissions: "builder:shared-components:read",
  },
});

const { exposeToChildren } = useBlade();
const { t } = useI18n({ useScope: "global" });
const { hasAccess } = usePermissions();
const renameTarget = ref<SharedComponent>();
const renameError = ref<string>();

const {
  items,
  loading,
  detailsLoading,
  loadError,
  totalCount,
  searchValue,
  selectedComponent,
  pagination,
  storeId,
  storeContextStatus,
  initialize,
  reload,
  search,
  retryLastLoad,
  selectComponent,
  refreshComponent,
  clearSelection,
  renameComponent,
  deleteComponent,
} = useSharedComponents();

const bladeTitle = computed(() => t("SHARED_COMPONENTS.TITLE"));
const isStoreContextReady = computed(() => storeContextStatus.value === "ready");
const isStoreContextInvalid = computed(() => ["missing", "notFound", "error"].includes(storeContextStatus.value));
const contentLoading = computed(() => loading.value || ["idle", "loading"].includes(storeContextStatus.value));
const canUpdate = computed(() => hasAccess("builder:shared-components:update") && isStoreContextReady.value);
const canDelete = computed(() => hasAccess("builder:shared-components:delete") && isStoreContextReady.value);
const canOpenDesigner = computed(
  () => hasAccess("builder:read") && isStoreContextReady.value && Boolean(storeId.value),
);
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

const bladeToolbar = ref<IBladeToolbar[]>([
  {
    id: "refresh",
    title: computed(() => t("SHARED_COMPONENTS.TOOLBAR.REFRESH")),
    icon: "lucide-refresh-cw",
    clickHandler: reloadContent,
  },
]);

const { notifyError, rename, confirmDelete } = useSharedComponentActions({
  canUpdate,
  canDelete,
  refreshComponent,
  renameComponent,
  deleteComponent,
  reload,
});

function openRenamePopup(component: SharedComponent) {
  if (!canUpdate.value || loading.value) {
    return;
  }

  clearRenameError();
  renameTarget.value = component;
}

function closeRenamePopup() {
  clearRenameError();
  renameTarget.value = undefined;
}

function clearRenameError() {
  renameError.value = undefined;
}

async function handleRename(name: string) {
  if (!renameTarget.value) {
    return;
  }

  clearRenameError();
  const result = await rename(renameTarget.value, name);

  if (result.succeeded) {
    closeRenamePopup();
    return;
  }

  renameError.value = result.errorMessage;
}

async function handleSelect(component: SharedComponent) {
  try {
    await selectComponent(component);
  } catch (error) {
    notifyError(error);
  }
}

async function handleSearch(keyword?: string) {
  try {
    await search(keyword);
  } catch (error) {
    notifyError(error);
  }
}

async function handleRetry() {
  try {
    await retryLastLoad();
  } catch (error) {
    notifyError(error);
  }
}

async function handleDelete(component: SharedComponent) {
  if (loading.value) {
    return;
  }

  await confirmDelete(component);
}

function openUsagePageDesigner(page: SharedComponentUsagePage) {
  if (!canOpenUsagePageDesigner(page)) {
    return;
  }

  openPageDesigner({
    groupId: page.id,
    storeId: storeId.value,
    cultureName: page.cultureName,
    status: page.status,
  });
}

function canOpenUsagePageDesigner(page: SharedComponentUsagePage): boolean {
  return (
    canOpenDesigner.value &&
    canOpenPageDesigner({
      groupId: page.id,
      storeId: storeId.value,
      status: page.status,
    })
  );
}

async function reloadContent() {
  try {
    await reload();
  } catch (error) {
    notifyError(error);
  }
}

onMounted(async () => {
  try {
    await initialize();
  } catch (error) {
    notifyError(error);
  }
});

exposeToChildren({ reload: reloadContent });
</script>
