<template>
  <div class="linked-components tw-flex tw-h-full tw-flex-col">
    <div
      v-if="isStoreContextInvalid"
      class="linked-components__store-context"
    >
      <VcIcon
        icon="lucide-triangle-alert"
        class="linked-components__store-context-icon"
      />
      <div class="linked-components__store-context-title">
        {{ storeContextTitle }}
      </div>
      <VcHint class="linked-components__store-context-text">
        {{ storeContextDescription }}
      </VcHint>
    </div>

    <div
      v-else
      class="linked-components__body"
    >
      <section
        class="linked-components__content"
        @click.self="clearSelection"
      >
        <LinkedComponentsTable
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

      <LinkedComponentDetails
        v-if="selectedComponent"
        :component="selectedComponent"
        :can-update="canUpdate"
        :can-delete="canDelete"
        :can-open-pages="canOpenPages"
        :loading="loading"
        :details-loading="detailsLoading"
        @close="clearSelection"
        @rename="openRenamePopup"
        @delete="handleDelete"
        @open-page="openUsagePage"
      />
    </div>
  </div>

  <RenameLinkedComponentPopup
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
import { useBlade, usePermissions } from "@vc-shell/framework";
import { VcHint, VcIcon } from "@vc-shell/framework/ui";
import type { LinkedComponent, LinkedComponentUsagePage } from "../types";
import { useLinkedComponentActions, useLinkedComponents } from "../composables";
import LinkedComponentDetails from "./LinkedComponentDetails.vue";
import LinkedComponentsTable from "./LinkedComponentsTable.vue";
import RenameLinkedComponentPopup from "./RenameLinkedComponentPopup.vue";

export interface ExposedLinkedComponentsContent {
  reload: () => Promise<void>;
}

const { t } = useI18n({ useScope: "global" });
const { hasAccess } = usePermissions();
const { openBlade } = useBlade();
const renameTarget = ref<LinkedComponent>();
const renameError = ref<string>();

const {
  items,
  loading,
  detailsLoading,
  totalCount,
  searchValue,
  selectedComponent,
  pagination,
  storeId,
  storeContextStatus,
  initialize,
  reload,
  search,
  selectComponent,
  refreshComponent,
  clearSelection,
  renameComponent,
  deleteComponent,
} = useLinkedComponents();

const isStoreContextReady = computed(() => storeContextStatus.value === "ready");
const isStoreContextInvalid = computed(() => ["missing", "notFound", "error"].includes(storeContextStatus.value));
const contentLoading = computed(() => loading.value || ["idle", "loading"].includes(storeContextStatus.value));
const canUpdate = computed(() => hasAccess("builder:linked-components:update") && isStoreContextReady.value);
const canDelete = computed(() => hasAccess("builder:linked-components:delete") && isStoreContextReady.value);
const canOpenPages = computed(() => hasAccess("builder:read") && isStoreContextReady.value);
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

const { notifyError, rename, confirmDelete } = useLinkedComponentActions({
  canUpdate,
  canDelete,
  refreshComponent,
  renameComponent,
  deleteComponent,
  reload,
});

function openRenamePopup(component: LinkedComponent) {
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

async function handleSelect(component: LinkedComponent) {
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

async function handleDelete(component: LinkedComponent) {
  if (loading.value) {
    return;
  }

  await confirmDelete(component);
}

function openUsagePage(page: LinkedComponentUsagePage) {
  if (!canOpenPages.value || !page.id || !storeId.value) {
    return;
  }

  openBlade({
    name: "PageDetails",
    param: page.id,
    options: {
      storeId: storeId.value,
    },
  });
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

defineExpose<ExposedLinkedComponentsContent>({
  reload: reloadContent,
});
</script>

<style lang="scss" scoped>
.linked-components {
  @apply tw-bg-[color:var(--neutrals-50)] tw-text-sm tw-leading-[18px] tw-text-[color:var(--neutrals-800)];

  button,
  input,
  select,
  textarea {
    font: inherit;
  }

  &__body {
    @apply tw-flex tw-min-h-0 tw-flex-1 tw-flex-col lg:tw-flex-row;
  }

  &__content {
    @apply tw-flex tw-min-h-0 tw-grow tw-basis-0 tw-flex-col;
  }

  &__store-context {
    @apply tw-flex tw-h-full tw-flex-1 tw-flex-col tw-items-center tw-justify-center tw-gap-3 tw-p-8 tw-text-center;
  }

  &__store-context-icon {
    @apply tw-text-[64px] tw-text-[color:var(--danger-500)];
  }

  &__store-context-title {
    @apply tw-text-lg tw-font-semibold;
  }

  &__store-context-text {
    @apply tw-max-w-[460px];
  }
}
</style>
