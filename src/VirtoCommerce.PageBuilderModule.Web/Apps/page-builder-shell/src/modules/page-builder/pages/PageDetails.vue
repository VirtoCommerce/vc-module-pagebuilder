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

        <VcBanner
          v-if="isLanguageVersion"
          variant="info"
          icon="lucide-languages"
        >
          {{ $t("PAGE_BUILDER.PAGES.DETAILS.LANGUAGE_VERSION_HINT") }}
        </VcBanner>

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
                       :disabled="isReadOnly || isLanguageVersion"
                       :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.NAME')"
                       required
                       @update:model-value="handleChange" />
            </Field>

            <Field
                   v-slot="{ errorMessage, handleChange, errors }"
                   name="permalink"
                   :rules="permalinkRules"
                   :model-value="item.permalink"
                   :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.PERMALINK')">
              <VcInput
                       v-model="item.permalink"
                       required
                       maxlength="2048"
                       :error="errors.length > 0"
                       :error-message="errorMessage"
                       :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.PERMALINK')"
                       :hint="fieldHints.Permalink"
                       :disabled="isReadOnly"
                       @update:model-value="handleChange">
                <template #prepend-inner>
                  <div
                       v-if="storeUrl"
                       data-test-id="permalink-prefix"
                       class="permalink-prefix tw-self-stretch tw-flex tw-items-center tw-text-sm">
                    {{ storeUrl }}
                  </div>
                </template>
              </VcInput>
            </Field>

            <VcBanner
              v-if="permalinkUsagesInLanguage.length"
              variant="warning"
              icon="lucide-triangle-alert"
              data-test-id="permalink-usages"
            >
              {{
                $t("PAGE_BUILDER.PAGES.DETAILS.WARNINGS.PERMALINK_USED", {
                  pages: formatPageNames(permalinkUsagesInLanguage),
                })
              }}
            </VcBanner>

            <VcSelect option-value="name" option-label="name" v-model="item.cultureName"
                      :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.CULTURE_NAME')" :options="loadCultureNamesAsync"
                      :clearable="false"
                      :disabled="isReadOnly || isLanguageVersion" />
          </VcCol>
        </VcCard>

        <PageAdvancedOptions
          v-model:item="item"
          :disabled="isReadOnly"
          :field-hints="fieldHints"
        />
      </VcForm>
    </VcContainer>
  </VcBlade>
</template>

<script lang="ts" setup>
import { computed, onMounted, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { Field } from "vee-validate";
import { watchDebounced } from "@vueuse/core";
import { IBladeToolbar, useBlade, useBladeForm, usePopup, notification } from "@vc-shell/framework";
import PageStatus from "../components/pageStatus.vue";
import useUrlParams from "../composables/useStoreParams";
import useAiAgentContextWithStore from "../composables/useAiAgentContextWithStore";
import { usePageBuilderDetails } from "../composables/usePageBuilderDetails";
import type { PageExportData } from "../composables/usePageContentApi";
import PageAdvancedOptions from "../components/pageAdvancedOptions.vue";
import { PageTypes } from "../composables";
import {
  inheritableFields,
  parseOverriddenFields,
  type InheritableField,
} from "../composables/useMultilanguageInheritance";
import {
  canUnpublishAll,
  formatPageNames,
  getPublishConflicts,
  getUsagesInLanguage,
} from "../utilities/permalinkUsages";
import type { PageBuilderPermalinkUsage } from "../../../api_client/virtocommerce.pagebuildermodule";
import { getPermalinkRules } from "../utilities/permalink";

import { VcBanner, VcBlade, VcCard, VcCol, VcContainer, VcForm, VcInput, VcSelect } from "@vc-shell/framework/ui";

defineBlade({
  name: "PageDetails",
  url: "/details",
});

const { t } = useI18n({ useScope: "global" });
const { showConfirmation, showError } = usePopup();
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
  loadPermalinkUsages,
  unpublishGroup,
  openDraftDesigner,
  downloadContent,
  clonePage,
  convertToMultilanguage,
} = usePageBuilderDetails({
  id: param.value as string | undefined,
  storeId: options.value?.storeId as string | undefined,
  importData: options.value?.importData as PageExportData | undefined,
});

const { canSave, isModified, setBaseline, markReady, formMeta } = useBladeForm({
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

// Other pages are allowed to use the same permalink, for another audience or period; the author is warned, and
// publishing checks whether the storefront would offer both pages to the same visitors.
const permalinkUsages = ref<PageBuilderPermalinkUsage[]>([]);
const permalinkUsagesInLanguage = computed(() => getUsagesInLanguage(permalinkUsages.value, item.value?.cultureName));
// Only the latest check counts: an earlier, slower response must not replace the warning for the current permalink.
let latestPermalinkUsagesRequest = 0;
watchDebounced(
  [() => item.value?.id, () => item.value?.permalink],
  async () => {
    const request = ++latestPermalinkUsagesRequest;
    let usages: PageBuilderPermalinkUsage[] = [];
    try {
      usages = await loadPermalinkUsages();
    } catch {
      // The warning is advisory and publishing checks again, so a failed check only leaves it out.
    }
    if (request === latestPermalinkUsagesRequest) {
      permalinkUsages.value = usages;
    }
  },
  { debounce: 500 },
);

// Returns false when the author cancels or the conflict has to be resolved on the other page first.
// If the check itself fails, publishing goes ahead: the server checks again and refuses a conflict.
async function publishWithConflictCheck(): Promise<boolean> {
  const conflicts = getPublishConflicts(await loadPermalinkUsages().catch(() => []));
  if (conflicts.blocking.length > 0) {
    const pages = formatPageNames(conflicts.blocking);
    if (!canUnpublishAll(conflicts.blocking)) {
      showError(t("PAGE_BUILDER.PAGES.ALERTS.PUBLISH_CONFLICT_HAS_CHANGES", { pages }));
      return false;
    }
    if (!(await showConfirmation(t("PAGE_BUILDER.PAGES.ALERTS.PUBLISH_CONFLICT", { pages })))) {
      return false;
    }
  }

  if (
    conflicts.possible.length > 0 &&
    !(await showConfirmation(
      t("PAGE_BUILDER.PAGES.ALERTS.PUBLISH_POSSIBLE_CONFLICT", { pages: formatPageNames(conflicts.possible) }),
    ))
  ) {
    return false;
  }
  await publishGroup(conflicts.blocking.length > 0);
  return true;
}

// item is replaced on every load and save, while edits change its fields, so this keeps the stored permalink.
const storedPermalink = ref<string>();
watch(
  item,
  (value) => {
    storedPermalink.value = value?.id ? value.permalink : undefined;
  },
  { immediate: true },
);
const permalinkRules = computed(() => getPermalinkRules(item.value?.permalink, storedPermalink.value));

// A language version of a Multilanguage Page: the name is shared and the language is fixed.
const isLanguageVersion = computed(() => !!item.value?.multilanguagePageId);

// As saved: the backend marks a field overridden when its value differs from the Multilanguage Page.
const fieldHints = computed<Partial<Record<InheritableField, string>>>(() => {
  if (!isLanguageVersion.value) {
    return {};
  }
  const overridden = parseOverriddenFields(item.value.overriddenFields);
  return Object.fromEntries(
    inheritableFields.map((field) => [
      field,
      overridden.includes(field)
        ? t("PAGE_BUILDER.PAGES.DETAILS.FIELD_HINTS.CUSTOM")
        : t("PAGE_BUILDER.PAGES.DETAILS.FIELD_HINTS.INHERITED"),
    ]),
  );
});

// Converting needs a second store language to translate into.
const hasSeveralStoreLanguages = ref(false);

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
        notification.success(t("PAGE_BUILDER.PAGES.ALERTS.DELETE_SUCCESS"));
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
    id: "convertToMultilanguage",
    icon: "lucide-languages",
    title: t("PAGE_BUILDER.PAGES.DETAILS.TOOLBAR.CONVERT"),
    isVisible: !!param.value && !isLanguageVersion.value && hasSeveralStoreLanguages.value,
    disabled: isReadOnly.value || isModified.value,
    clickHandler: async () => {
      if (await showConfirmation(t("PAGE_BUILDER.PAGES.ALERTS.CONVERT_CONFIRMATION"))) {
        const root = await convertToMultilanguage();
        notification.success(t("PAGE_BUILDER.PAGES.ALERTS.CONVERT_SUCCESS"));
        callParent("reload");
        callParent("onItemClick", { type: PageTypes.Multilanguage, id: root.id });
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
      if (!(await publishWithConflictCheck())) {
        return;
      }
      notification.success(t("PAGE_BUILDER.PAGES.ALERTS.PUBLISH_SUCCESS"));
      setBaseline();
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
      notification.success(t("PAGE_BUILDER.PAGES.ALERTS.UNPUBLISH_SUCCESS"));
      setBaseline();
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
  if (options.value?.importData) {
    markReady();
  } else {
    setBaseline();
  }
  storeUrl.value = await getStoreUrl();
  hasSeveralStoreLanguages.value = (await getLanguages()).length > 1;
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
