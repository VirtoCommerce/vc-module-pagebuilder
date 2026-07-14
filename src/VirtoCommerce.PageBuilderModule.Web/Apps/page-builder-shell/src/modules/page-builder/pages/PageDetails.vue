<template>
  <VcBlade
    :loading="loading"
    :title="bladeTitle"
    width="50%"
    :toolbar-items="bladeToolbar"
  >
    <VcContainer>
      <VcForm class="tw-flex tw-flex-col tw-gap-4">
        <PageStatus :item="item" />

        <VcCard
                class="tw-p-4"
                :header="$t('PAGE_BUILDER.PAGES.DETAILS.SECTIONS.BASIC_INFORMATION')">
          <VcCol class="tw-gap-4">
            <Field
                   v-slot="{ errorMessage, handleChange, errors }"
                   name="name"
                   :model-value="item.name"
                   :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.NAME')"
                   rules="required">
              <VcInput
                       v-model="item.name"
                       :error="errors.length > 0"
                       :error-message="errorMessage"
                       :disabled="isReadOnly"
                       :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.NAME')"
                       required
                       @update:model-value="handleChange" />
            </Field>

            <Field
                   v-slot="{ errorMessage, handleChange, errors }"
                   name="permalink"
                   rules="required"
                   :model-value="item.permalink"
                   :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.PERMALINK')">
              <VcInput
                       v-model="item.permalink"
                       required
                       :error="errors.length > 0"
                       :error-message="errorMessage"
                       :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.PERMALINK')"
                       :disabled="isReadOnly"
                       @update:model-value="handleChange">
                <template #prepend-inner>
                  <div
                       v-if="storeUrl"
                       class="permalink-prefix tw-self-stretch tw-flex tw-items-center tw-text-sm">
                    {{ storeUrl }}
                  </div>
                </template>
              </VcInput>
            </Field>

            <VcSelect option-value="name" option-label="name" v-model="item.cultureName"
                      :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.CULTURE_NAME')" :options="loadCultureNamesAsync"
                      :clearable="false"
                      :disabled="isReadOnly" />
          </VcCol>
        </VcCard>

        <VcCard class="tw-p-4" :header="$t('PAGE_BUILDER.PAGES.DETAILS.SECTIONS.ADVANCED_OPTIONS')">
          <VcCol class="tw-gap-4">
            <VcCard
              is-collapsable
              is-collapsed
            >
              <template #header>
                <CardHeader
                  icon="lucide-users"
                  :tag-text="
                    item.visibility
                      ? $t('PAGE_BUILDER.PAGES.DETAILS.SECTIONS.PERSONALIZATION_ACCESS_CONTROL.VISIBILITY_TAG')
                      : undefined
                  "
                  :title="$t('PAGE_BUILDER.PAGES.DETAILS.SECTIONS.PERSONALIZATION_ACCESS_CONTROL.TITLE')"
                  :description="$t('PAGE_BUILDER.PAGES.DETAILS.SECTIONS.PERSONALIZATION_ACCESS_CONTROL.DESCRIPTION')"
                />
              </template>

              <VcCol class="tw-gap-4 tw-p-4">
                <SwitchRow
                  :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.VISIBILITY')"
                  :hint="$t('PAGE_BUILDER.PAGES.DETAILS.TOOLTIPS.VISIBILITY')"
                  :model-value="item.visibility"
                  @update:model-value="item.visibility = $event"
                />

                <VcSelect v-model="itemUserGroups" :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.USER_GROUPS')"
                          :options="loadUserGroups" option-value="name" option-label="name" searchable multiple
                          :clearable="false"
                          :disabled="isReadOnly" />

                <VcSelect v-model="item.organizationId" :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.ORGANIZATION')"
                          :options="loadOrganizations" option-value="id" option-label="name" searchable
                          :clearable="true"
                          :disabled="isReadOnly" />
              </VcCol>
            </VcCard>

            <VcCard
              is-collapsable
              is-collapsed
            >
              <template #header>
                <CardHeader
                  icon="lucide-calendar-range"
                  :tag-text="isScheduled ? $t('PAGE_BUILDER.PAGES.DETAILS.SECTIONS.SCHEDULING.TAG_TEXT') : ''"
                  :title="$t('PAGE_BUILDER.PAGES.DETAILS.SECTIONS.SCHEDULING.TITLE')"
                  :description="$t('PAGE_BUILDER.PAGES.DETAILS.SECTIONS.SCHEDULING.DESCRIPTION')"
                />
              </template>
              <VcRow class="tw-gap-4 tw-p-4">
                <VcInput
                  v-model="item.startDate"
                  class="tw-flex-1"
                  type="datetime-local"
                  clearable
                  :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.START_DATE')"
                  :hint="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.START_DATE_HINT')"
                  :disabled="isReadOnly"
                />

                <VcInput v-model="item.endDate" class="tw-flex-1" type="datetime-local" clearable
                         :hint="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.END_DATE_HINT')"
                         :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.END_DATE')" :disabled="isReadOnly" />
              </VcRow>
            </VcCard>
          </VcCol>
        </VcCard>
      </VcForm>
    </VcContainer>
  </VcBlade>
</template>

<script lang="ts" setup>
import { computed, onMounted, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { Field } from "vee-validate";
import { IBladeToolbar, useBlade, useBladeForm, usePopup, notification } from "@vc-shell/framework";
import PageStatus from "../components/pageStatus.vue";
import useUrlParams from "../composables/useStoreParams";
import useAiAgentContextWithStore from "../composables/useAiAgentContextWithStore";
import { usePageBuilderDetails } from "../composables/usePageBuilderDetails";
import type { PageExportData } from "../composables/usePageContentApi";
import CardHeader from "./../components/cardHeader.vue";
import SwitchRow from "./../components/switchRow.vue";

import { VcBlade, VcCard, VcCol, VcContainer, VcForm, VcInput, VcRow, VcSelect } from "@vc-shell/framework/ui";

defineBlade({
  name: "PageDetails",
  url: "/details",
});

const { t } = useI18n({ useScope: "global" });
const { showConfirmation } = usePopup();
const { param, options, callParent, closeSelf, exposeToChildren } = useBlade();
const { getStoreUrl, getLanguages } = useUrlParams();

const {
  item,
  status,
  isReadOnly,
  loading,
  loadGroup,
  saveGroup,
  deleteGroup,
  publishGroup,
  unpublishGroup,
  openDraftDesigner,
  downloadContent,
  clonePage,
  loadUserGroups,
  loadOrganizations,
} = usePageBuilderDetails({
  id: param.value as string | undefined,
  storeId: options.value?.storeId as string | undefined,
  importData: options.value?.importData as PageExportData | undefined,
});

const { canSave, isModified, setBaseline, formMeta } = useBladeForm({
  data: item,
  closeConfirmMessage: () => t("PAGE_BUILDER.PAGES.ALERTS.CLOSE_CONFIRMATION"),
  canSaveOverride: computed(() => !isReadOnly.value),
});

// WORKAROUND: push storeId + current page into the AI agent context so pagebuilder
// tools can read them. See docs/storeId-missing-in-ai-context.md for the proper fix.
const aiContextItem = computed(() =>
  item.value?.id
    ? { id: item.value.id, objectType: "pagebuilder.page", name: item.value.name }
    : null,
);
useAiAgentContextWithStore({ dataRef: aiContextItem });

watch(
  item,
  () => {
    if (item.value && !options.value?.importData) setBaseline();
  },
  { once: true },
);

const bladeTitle = computed(() => {
  if (param.value || item.value?.name) {
    return t("PAGE_BUILDER.PAGES.DETAILS.TITLE.DETAILS", { name: item.value?.name });
  }
  return t("PAGE_BUILDER.PAGES.DETAILS.TITLE.NEW");
});

const storeUrl = ref<string | null>(null);

const isScheduled = computed(() => {
  return !!item.value.startDate || !!item.value.endDate;
});

function parseUserGroups(str: string | undefined): string[] {
  return str ? str.split(",").filter((x) => !!x) : [];
}
function serializeUserGroups(groups: string[]): string {
  return groups.filter((x) => !!x).join(",");
}
const itemUserGroups = computed<string[]>({
  get: () => {
    return parseUserGroups(item.value.userGroups);
  },
  set: (val: string[]) => {
    item.value.userGroups = serializeUserGroups(val);
  },
});

// Toolbar
const bladeToolbar = computed((): IBladeToolbar[] => [
  {
    id: "save",
    icon: "lucide-save",
    title: t("PAGE_BUILDER.PAGES.DETAILS.TOOLBAR.SAVE"),
    disabled: !canSave.value,
    clickHandler: async () => {
      await handleSave();
      notification.success(t("PAGE_BUILDER.PAGES.ALERTS.SAVE_SUCCESS"));
    },
  },
  {
    id: "delete",
    icon: "lucide-trash-2",
    title: t("PAGE_BUILDER.PAGES.DETAILS.TOOLBAR.DELETE"),
    disabled: !param.value || isReadOnly.value,
    clickHandler: async () => {
      if (await showConfirmation(t("PAGE_BUILDER.PAGES.ALERTS.DELETE"))) {
        await deleteGroup();
        callParent("reload");
        closeSelf();
      }
    },
  },
  {
    id: "openPageDesigner",
    icon: "lucide-crop",
    title: t("PAGE_BUILDER.PAGES.DETAILS.TOOLBAR.DESIGNER"),
    disabled: !param.value || isReadOnly.value,
    clickHandler: () => {
      openDraftDesigner();
    },
  },
  {
    id: "downloadContent",
    icon: "lucide-download",
    title: t("PAGE_BUILDER.PAGES.DETAILS.TOOLBAR.DOWNLOAD_CONTENT"),
    disabled: !param.value,
    clickHandler: async () => {
      try {
        await downloadContent();
        notification.success(t("PAGE_BUILDER.PAGES.ALERTS.DOWNLOAD_SUCCESS"));
      } catch {
        // error is handled by useAsync / global error handler
      }
    },
  },
  {
    id: "clonePage",
    icon: "lucide-copy",
    title: t("PAGE_BUILDER.PAGES.DETAILS.TOOLBAR.CLONE"),
    disabled: !param.value || isReadOnly.value,
    clickHandler: async () => {
      if (loading.value) return;
      const cloned = await clonePage();
      if (cloned?.id) {
        notification.success(t("PAGE_BUILDER.PAGES.ALERTS.CLONE_SUCCESS"));
        callParent("reload");
        callParent("onItemClick", cloned);
      }
    },
  },
  {
    id: "publishPage",
    icon: "lucide-file-text",
    title: t("PAGE_BUILDER.PAGES.DETAILS.TOOLBAR.PUBLISH"),
    isVisible: !!param.value && status.value?.hasChanges === true,
    disabled: isReadOnly.value || isModified.value || !formMeta.value.valid,
    clickHandler: async () => {
      await publishGroup();
      callParent("reload");
    },
  },
  {
    id: "unpublishPage",
    icon: "lucide-file",
    title: t("PAGE_BUILDER.PAGES.DETAILS.TOOLBAR.UNPUBLISH"),
    isVisible: !!param.value && status.value?.hasChanges === false,
    disabled: isReadOnly.value,
    clickHandler: async () => {
      await unpublishGroup();
      callParent("reload");
    },
  },
]);

// Methods
async function loadCultureNamesAsync() {
  const langs = await getLanguages();
  return {
    totalCount: langs.length,
    results: langs.map((x) => ({ name: x })),
  };
}

async function handleSave() {
  const group = await saveGroup();
  setBaseline();

  callParent("reload");

  if (item.value.id || group.id) {
    callParent("onItemClick", group.id ? group : item.value);
  }
}

// Lifecycle
onMounted(async () => {
  await loadGroup();
  setBaseline();
  storeUrl.value = await getStoreUrl();
});
</script>

<style scoped lang="scss">
.permalink-prefix {
  margin-left: calc(-1.1 * var(--input-padding));
  padding-inline: var(--input-padding);
  border-start-start-radius: var(--input-border-radius);
  border-end-start-radius: var(--input-border-radius);
  background-color: var(--input-disabled-bg-color);
}
</style>
