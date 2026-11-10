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

            <VcSelect option-value="name" option-label="name" v-model="item.cultureName"
                      :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.CULTURE_NAME')" :options="loadCultureNamesAsync"
                      :clearable="false"
                      :disabled="isReadOnly || isLanguageVersion" />
          </VcCol>
        </VcCard>

        <VcCard
                v-if="item.id"
                class="tw-p-4"
                :header="$t('PAGE_BUILDER.PAGES.DETAILS.SECTIONS.SEO')">
          <VcCol class="tw-gap-4">
            <div class="seo-preview">
              <span class="seo-preview__title">{{ seo.pageTitle || item.name }}</span>
              <span class="seo-preview__url">{{ pageUrl }}</span>
              <span
                    v-if="seo.metaDescription"
                    class="seo-preview__description">{{ seo.metaDescription }}</span>
            </div>
            <VcInput
                     v-model="seo.pageTitle"
                     :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.SEO_TITLE')"
                     :placeholder="item.name"
                     :disabled="isReadOnly" />
            <VcTextarea
                        v-model="seo.metaDescription"
                        :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.SEO_DESCRIPTION')"
                        :disabled="isReadOnly" />
            <VcInput
                     v-model="seo.metaKeywords"
                     :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.SEO_KEYWORDS')"
                     :disabled="isReadOnly" />
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
import { IBladeToolbar, useBlade, useBladeForm, usePopup, notification } from "@vc-shell/framework";
import PageStatus from "../components/pageStatus.vue";
import useUrlParams from "../composables/useStoreParams";
import useAiAgentContextWithStore from "../composables/useAiAgentContextWithStore";
import { notifyPagesChanged } from "../composables/usePageBuilderList";
import { usePageBuilderDetails } from "../composables/usePageBuilderDetails";
import type { PageExportData } from "../composables/usePageContentApi";
import PageAdvancedOptions from "../components/pageAdvancedOptions.vue";
import { usePermalinkUsage } from "../composables/usePermalinkUsage";
import {
  inheritableFields,
  parseOverriddenFields,
  type InheritableField,
} from "../composables/useMultilanguageInheritance";
import { isPermalinkCheckNeeded, type IPermalinkTarget } from "../composables/useMultilanguagePageDetails/helpers";
import { getPermalinkRules } from "../utilities/permalink";

import { VcBanner, VcBlade, VcCard, VcCol, VcContainer, VcForm, VcInput, VcSelect, VcTextarea } from "@vc-shell/framework/ui";

defineBlade({
  name: "PageBuilderPageDetails",
  url: "/page-builder-page",
});

const { t } = useI18n({ useScope: "global" });
const { showConfirmation } = usePopup();
const { param, options, closeSelf, replaceWith } = useBlade();
const { storeId, setStoreId, getStoreUrl, getLanguages } = useUrlParams();

const {
  item,
  seo,
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
  convertToMultilanguage,
} = usePageBuilderDetails({
  id: param.value as string | undefined,
  storeId: options.value?.storeId as string | undefined,
  importData: options.value?.importData as PageExportData | undefined,
});

const form = computed({
  get: () => ({ item: item.value, seo: seo.value }),
  set: (value) => {
    item.value = value.item;
    seo.value = value.seo;
  },
});
const { canSave, isModified, setBaseline, markReady, formMeta } = useBladeForm({
  data: form,
  closeConfirmMessage: () => t("PAGE_BUILDER.PAGES.ALERTS.CLOSE_CONFIRMATION"),
  canSaveOverride: computed(() => !isReadOnly.value),
});

// Workaround: the AI agent contract has no storeId, so the page tools read it and the current page from this context.
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
const pageUrl = computed(() => `${(storeUrl.value ?? "").replace(/\/$/, "")}/${(item.value.permalink ?? "").replace(/^\//, "")}`);

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

const { confirmPermalinkUsage } = usePermalinkUsage();
// Permalink and language as saved: an unchanged permalink was already confirmed.
const savedPermalinkTarget = ref<IPermalinkTarget>();

function rememberPermalinkTarget() {
  savedPermalinkTarget.value = item.value?.id
    ? { permalink: item.value.permalink, cultureName: item.value.cultureName }
    : undefined;
}

async function confirmPagePermalinkUsage(): Promise<boolean> {
  if (!isPermalinkCheckNeeded(savedPermalinkTarget.value, item.value)) {
    return true;
  }

  return confirmPermalinkUsage({
    storeId: item.value.storeId ?? (options.value?.storeId as string | undefined),
    permalink: item.value.permalink,
    cultureNames: item.value.cultureName ? [item.value.cultureName] : [],
    excludeGroupIds: item.value.id ? [item.value.id] : [],
  });
}

// Converting needs a second store language to translate into.
const hasSeveralStoreLanguages = ref(false);

const bladeToolbar = computed((): IBladeToolbar[] => [
  {
    id: "save",
    icon: "lucide-save",
    title: t("PAGE_BUILDER.PAGES.DETAILS.TOOLBAR.SAVE"),
    disabled: !canSave.value,
    clickHandler: async () => {
      if (!(await confirmPagePermalinkUsage())) {
        return;
      }
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
        notifyPagesChanged();
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
      openDraftDesigner(async () => {
        // The designer saves on its own, so show what it left unless this blade has edits of its own.
        if (!isModified.value) {
          await loadGroup();
          setBaseline();
        }
        notifyPagesChanged();
      });
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
        // useAsync and the global error handler report the error.
      }
    },
  },
  {
    id: "clonePage",
    icon: "lucide-copy",
    title: t("PAGE_BUILDER.PAGES.DETAILS.TOOLBAR.CLONE"),
    disabled: !param.value || isReadOnly.value || isModified.value,
    clickHandler: async () => {
      if (loading.value) return;
      const cloned = await clonePage();
      if (cloned?.id) {
        notification.success(t("PAGE_BUILDER.PAGES.ALERTS.CLONE_SUCCESS"));
        notifyPagesChanged();
        await openPage(cloned.id);
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
        notifyPagesChanged();
        await replaceWith({ name: "MultilanguagePageDetails", param: root.id, options: { storeId: options.value?.storeId } });
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
      notification.success(t("PAGE_BUILDER.PAGES.ALERTS.PUBLISH_SUCCESS"));
      setBaseline();
      notifyPagesChanged();
    },
  },
  {
    id: "unpublishPage",
    icon: "lucide-file",
    title: t("PAGE_BUILDER.PAGES.DETAILS.TOOLBAR.UNPUBLISH"),
    isVisible: !!param.value && status.value?.hasChanges === false,
    disabled: isReadOnly.value || isModified.value,
    clickHandler: async () => {
      await unpublishGroup();
      notification.success(t("PAGE_BUILDER.PAGES.ALERTS.UNPUBLISH_SUCCESS"));
      setBaseline();
      notifyPagesChanged();
    },
  },
]);

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
  rememberPermalinkTarget();
  notifyPagesChanged();

  if (!param.value && group.id) {
    await openPage(group.id);
  }
}

async function openPage(id: string | undefined) {
  if (id) {
    await replaceWith({ name: "PageBuilderPageDetails", param: id, options: { storeId: options.value?.storeId } });
  }
}

onMounted(async () => {
  await loadGroup();
  rememberPermalinkTarget();
  if (options.value?.importData) {
    markReady();
  } else {
    setBaseline();
  }
  // A blade opened by a link or a reload has no opener to set the store, so the page names its own.
  if (!storeId.value && item.value.storeId) setStoreId(item.value.storeId);
  storeUrl.value = await getStoreUrl();
  hasSeveralStoreLanguages.value = (await getLanguages()).length > 1;
});
</script>

<style scoped lang="scss">
.seo-preview {
  display: flex;
  flex-direction: column;
  gap: 2px;
  padding: 12px 16px;
  border: 1px solid var(--neutrals-200);
  border-radius: 8px;
  font-size: 13px;
  overflow-wrap: anywhere;

  &__title {
    color: var(--primary-600);
    font-size: 16px;
  }

  &__url {
    color: var(--success-700);
  }

  &__description {
    color: var(--neutrals-600);
  }
}

.permalink-prefix {
  margin-left: calc(-1.1 * var(--input-padding));
  padding-inline: var(--input-padding);
  border-start-start-radius: var(--input-border-radius);
  border-end-start-radius: var(--input-border-radius);
  background-color: var(--input-disabled-bg-color);
}
</style>
