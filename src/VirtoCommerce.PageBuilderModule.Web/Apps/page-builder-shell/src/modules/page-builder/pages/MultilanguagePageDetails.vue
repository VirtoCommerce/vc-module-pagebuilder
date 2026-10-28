<template>
  <VcBlade
    :loading="loading"
    :title="bladeTitle"
    width="50%"
    :toolbar-items="bladeToolbar"
  >
    <VcContainer>
      <VcForm class="tw-flex tw-flex-col tw-gap-4">
        <VcCard
          class="tw-p-4"
          :header="$t('PAGE_BUILDER.PAGES.DETAILS.SECTIONS.BASIC_INFORMATION')"
        >
          <VcCol class="tw-gap-4">
            <Field
              v-slot="{ errorMessage, handleChange, errors }"
              name="name"
              :model-value="item.name"
              :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.NAME')"
              rules="required"
            >
              <VcInput
                v-model="item.name"
                :error="errors.length > 0"
                :error-message="errorMessage"
                :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.NAME')"
                :hint="$t('PAGE_BUILDER.MULTILANGUAGE.FIELDS.NAME_HINT')"
                required
                @update:model-value="handleChange"
              />
            </Field>

            <Field
              v-slot="{ errorMessage, handleChange, errors }"
              name="permalink"
              :rules="permalinkRules"
              :model-value="item.permalink"
              :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.PERMALINK')"
            >
              <VcInput
                v-model="item.permalink"
                required
                maxlength="2048"
                :error="errors.length > 0"
                :error-message="errorMessage"
                :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.PERMALINK')"
                :hint="$t('PAGE_BUILDER.MULTILANGUAGE.FIELDS.PERMALINK_HINT')"
                @update:model-value="handleChange"
              >
                <template #prepend-inner>
                  <div
                    v-if="storeUrl"
                    class="permalink-prefix tw-self-stretch tw-flex tw-items-center tw-text-sm"
                  >
                    {{ storeUrl }}
                  </div>
                </template>
              </VcInput>
            </Field>

            <VcSelect
              v-model="item.defaultCultureName"
              option-value="name"
              option-label="name"
              :label="$t('PAGE_BUILDER.MULTILANGUAGE.FIELDS.DEFAULT_LANGUAGE')"
              :hint="$t('PAGE_BUILDER.MULTILANGUAGE.FIELDS.DEFAULT_LANGUAGE_HINT')"
              :options="loadCultureNames"
              :clearable="false"
            />
          </VcCol>
        </VcCard>

        <VcCard
          v-if="!isNew"
          class="tw-p-4"
          :header="$t('PAGE_BUILDER.MULTILANGUAGE.LANGUAGES.TITLE')"
        >
          <table class="languages-table">
            <thead>
              <tr>
                <th>{{ $t("PAGE_BUILDER.MULTILANGUAGE.LANGUAGES.LANGUAGE") }}</th>
                <th>{{ $t("PAGE_BUILDER.PAGES.DETAILS.FIELDS.PERMALINK") }}</th>
                <th>{{ $t("PAGE_BUILDER.PAGES.LIST.TABLE.HEADER.STATUS") }}</th>
                <th>{{ $t("PAGE_BUILDER.MULTILANGUAGE.LANGUAGES.ACTIONS") }}</th>
              </tr>
            </thead>
            <tbody>
              <template
                v-for="language in languages"
                :key="language.cultureName"
              >
                <tr>
                  <td>
                    <span class="tw-font-semibold">{{ language.cultureName }}</span>
                    <VcHint v-if="language.isDefault">{{ $t("PAGE_BUILDER.MULTILANGUAGE.LANGUAGES.DEFAULT") }}</VcHint>
                    <VcHint v-if="!language.isStoreLanguage">{{
                      $t("PAGE_BUILDER.MULTILANGUAGE.LANGUAGES.NOT_IN_STORE")
                    }}</VcHint>
                  </td>
                  <td>
                    <template v-if="language.page">
                      <code>{{ language.page.permalink }}</code>
                      <Tag
                        class="tw-ml-2"
                        :text="
                          isFieldOverridden(language.page.overriddenFields, 'Permalink')
                            ? $t('PAGE_BUILDER.MULTILANGUAGE.LANGUAGES.CUSTOM')
                            : $t('PAGE_BUILDER.MULTILANGUAGE.LANGUAGES.INHERITED')
                        "
                      />
                    </template>
                    <template v-else>—</template>
                  </td>
                  <td>
                    <LanguagePills
                      :languages="[language]"
                      with-status
                    />
                  </td>
                  <td>
                    <div class="languages-table__actions">
                      <template v-if="language.page">
                        <VcButton
                          size="sm"
                          variant="secondary"
                          :aria-label="actionLabel('PAGE_BUILDER.MULTILANGUAGE.LANGUAGES.OPEN', language)"
                          @click="openLanguage(language)"
                        >
                          {{ $t("PAGE_BUILDER.MULTILANGUAGE.LANGUAGES.OPEN") }}
                        </VcButton>
                        <VcButton
                          size="sm"
                          variant="secondary"
                          :disabled="language.status === 'Archived'"
                          :aria-label="actionLabel('PAGE_BUILDER.PAGES.DETAILS.TOOLBAR.DESIGNER', language)"
                          @click="openDesigner(language)"
                        >
                          {{ $t("PAGE_BUILDER.PAGES.DETAILS.TOOLBAR.DESIGNER") }}
                        </VcButton>
                        <VcButton
                          v-if="language.isOutdated"
                          size="sm"
                          variant="secondary"
                          :aria-label="actionLabel('PAGE_BUILDER.MULTILANGUAGE.LANGUAGES.MARK_UP_TO_DATE', language)"
                          @click="onMarkUpToDate(language)"
                        >
                          {{ $t("PAGE_BUILDER.MULTILANGUAGE.LANGUAGES.MARK_UP_TO_DATE") }}
                        </VcButton>
                        <VcButton
                          size="sm"
                          variant="secondary"
                          :aria-label="actionLabel('PAGE_BUILDER.MULTILANGUAGE.LANGUAGES.UNLINK', language)"
                          @click="onUnlink(language)"
                        >
                          {{ $t("PAGE_BUILDER.MULTILANGUAGE.LANGUAGES.UNLINK") }}
                        </VcButton>
                      </template>
                      <template v-else-if="language.isStoreLanguage">
                        <VcButton
                          size="sm"
                          :aria-label="actionLabel('PAGE_BUILDER.MULTILANGUAGE.LANGUAGES.CREATE', language)"
                          @click="onCreateLanguage(language)"
                        >
                          {{ $t("PAGE_BUILDER.MULTILANGUAGE.LANGUAGES.CREATE") }}
                        </VcButton>
                        <VcButton
                          size="sm"
                          variant="secondary"
                          :aria-label="actionLabel('PAGE_BUILDER.MULTILANGUAGE.LANGUAGES.LINK', language)"
                          @click="linkCultureName = language.cultureName"
                        >
                          {{ $t("PAGE_BUILDER.MULTILANGUAGE.LANGUAGES.LINK") }}
                        </VcButton>
                      </template>
                    </div>
                  </td>
                </tr>
                <tr
                  v-if="linkCultureName === language.cultureName"
                  class="languages-table__link"
                >
                  <td colspan="4">
                    <LinkPageSelect
                      :culture-name="language.cultureName!"
                      :find-pages="findLandingPages"
                      @close="linkCultureName = undefined"
                      @link="onLink"
                    />
                  </td>
                </tr>
              </template>
            </tbody>
          </table>
        </VcCard>

        <PageAdvancedOptions
          v-model:item="item"
          :description="$t('PAGE_BUILDER.MULTILANGUAGE.FIELDS.ADVANCED_OPTIONS_HINT')"
        />
      </VcForm>
    </VcContainer>

    <OverridesConflictPopup
      v-if="conflicts.length"
      :conflicts="conflicts"
      @close="conflicts = []"
      @apply-all="onApplyToAll"
      @apply-inherited="onApplyToInherited"
    />
  </VcBlade>
</template>

<script lang="ts" setup>
import { computed, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { Field } from "vee-validate";
import { IBladeToolbar, notification, useBlade, useBladeForm, usePopup } from "@vc-shell/framework";
import {
  VcButton,
  VcBlade,
  VcCard,
  VcCol,
  VcContainer,
  VcForm,
  VcHint,
  VcInput,
  VcSelect,
} from "@vc-shell/framework/ui";
import {
  GroupedPageBuilderPage,
  MultilanguagePageBatchResult,
  MultilanguagePageLanguage,
} from "../../../api_client/virtocommerce.pagebuildermodule";
import useUrlParams from "../composables/useStoreParams";
import { PageTypes } from "../composables";
import { useMultilanguagePageDetails } from "../composables/useMultilanguagePageDetails";
import { openPageDesigner } from "../../../utilities/pageDesigner";
import PageAdvancedOptions from "../components/pageAdvancedOptions.vue";
import LanguagePills from "../components/languagePills.vue";
import LinkPageSelect from "../components/linkPageSelect.vue";
import OverridesConflictPopup, { type IOverridesConflict } from "../components/overridesConflictPopup.vue";
import { usePermalinkUsage } from "../composables/usePermalinkUsage";
import {
  getChangedFields,
  isFieldOverridden,
  parseOverriddenFields,
  type IInheritableSettings,
} from "../composables/useMultilanguageInheritance";
import {
  hasLanguageVersions,
  isMultilanguageRowArchived,
  isPermalinkCheckNeeded,
  summarizeBatchResult,
} from "../composables/useMultilanguagePageDetails/helpers";
import Tag from "../components/tag.vue";
import { getPermalinkRules } from "../utilities/permalink";

defineBlade({
  name: "MultilanguagePageDetails",
  url: "/multilanguage-details",
});

const { t } = useI18n({ useScope: "global" });
const { showConfirmation } = usePopup();
const { param, options, activeChildParam, callParent, closeSelf, closeChildren, openBlade, exposeToChildren } =
  useBlade();
const { storeId, getStoreUrl, getLanguages, getDefaultLanguage } = useUrlParams();

const {
  item,
  languages,
  isNew,
  loading,
  load,
  loadLanguages,
  save,
  remove,
  publishAll,
  archiveAll,
  createLanguage,
  link,
  unlink,
  markUpToDate,
  resetOverrides,
  clone,
  findLandingPages,
} = useMultilanguagePageDetails({
  id: param.value as string | undefined,
  storeId: (options.value?.storeId as string | undefined) ?? storeId.value ?? undefined,
});

const { canSave, isModified, setBaseline } = useBladeForm({
  data: item,
  closeConfirmMessage: () => t("PAGE_BUILDER.PAGES.ALERTS.CLOSE_CONFIRMATION"),
});

const storeUrl = ref<string | null>(null);
// Settings as last loaded or saved: a change of an inherited field may conflict with language overrides.
const savedSettings = ref<IInheritableSettings>({});
const conflicts = ref<IOverridesConflict[]>([]);
const { confirmPermalinkUsage } = usePermalinkUsage();
const linkCultureName = ref<string>();

const bladeTitle = computed(() =>
  isNew.value
    ? t("PAGE_BUILDER.MULTILANGUAGE.TITLE.NEW")
    : t("PAGE_BUILDER.MULTILANGUAGE.TITLE.DETAILS", { name: item.value.name }),
);

// Like the Landing Page blade: a permalink saved before the rule existed does not block saving other fields.
const permalinkRules = computed(() => getPermalinkRules(item.value.permalink, savedSettings.value.permalink));

const hasVersions = computed(() => hasLanguageVersions(languages.value));
const isArchived = computed(() => isMultilanguageRowArchived(languages.value));

const bladeToolbar = computed((): IBladeToolbar[] => [
  {
    id: "save",
    icon: "lucide-save",
    title: t("PAGE_BUILDER.PAGES.DETAILS.TOOLBAR.SAVE"),
    disabled: !canSave.value,
    clickHandler: async () => {
      // Language version blades show the values the save changes; saved later, they would turn them into overrides.
      if (!(await closeChildrenOrCancel()) || !(await confirmRootPermalinkUsage())) {
        return;
      }
      conflicts.value = findConflicts();
      if (!conflicts.value.length) {
        await saveRoot();
      }
    },
  },
  {
    id: "publishAll",
    icon: "lucide-rocket",
    title: t("PAGE_BUILDER.MULTILANGUAGE.TOOLBAR.PUBLISH_ALL"),
    isVisible: !isNew.value,
    disabled: !hasVersions.value || isModified.value,
    clickHandler: async () => {
      // An open language version blade would keep showing the pre-publish status.
      if (!(await closeChildrenOrCancel())) {
        return;
      }
      notifyBatchResult(await publishAll(), "PUBLISH_ALL_RESULT");
      callParent("reload");
    },
  },
  {
    id: "archiveAll",
    icon: "lucide-archive",
    title: t("PAGE_BUILDER.MULTILANGUAGE.TOOLBAR.ARCHIVE_ALL"),
    isVisible: !isNew.value,
    disabled: !hasVersions.value || isArchived.value,
    clickHandler: async () => {
      if (
        (await showConfirmation(t("PAGE_BUILDER.MULTILANGUAGE.ALERTS.ARCHIVE_ALL"))) &&
        (await closeChildrenOrCancel())
      ) {
        notifyBatchResult(await archiveAll(), "ARCHIVE_ALL_RESULT");
        callParent("reload");
      }
    },
  },
  {
    id: "clone",
    icon: "lucide-copy",
    title: t("PAGE_BUILDER.PAGES.DETAILS.TOOLBAR.CLONE"),
    isVisible: !isNew.value,
    disabled: isModified.value,
    clickHandler: async () => {
      if (loading.value) {
        return;
      }
      const cloned = await clone();
      notification.success(t("PAGE_BUILDER.PAGES.ALERTS.CLONE_SUCCESS"));
      callParent("reload");
      callParent("onItemClick", { type: PageTypes.Multilanguage, id: cloned.id });
    },
  },
  {
    id: "delete",
    icon: "lucide-trash-2",
    title: t("PAGE_BUILDER.MULTILANGUAGE.TOOLBAR.DELETE"),
    // A page with language versions is archived (Archive all), like a Landing Page.
    isVisible: !isNew.value && !hasVersions.value,
    clickHandler: async () => {
      if (await showConfirmation(t("PAGE_BUILDER.MULTILANGUAGE.ALERTS.DELETE"))) {
        await remove();
        notification.success(t("PAGE_BUILDER.MULTILANGUAGE.ALERTS.DELETE_SUCCESS"));
        callParent("reload");
        closeSelf();
      }
    },
  },
]);

// Closing asks about unsaved changes; an action must not run while the user keeps a child blade open.
async function closeChildrenOrCancel(): Promise<boolean> {
  await closeChildren();
  return !activeChildParam.value;
}

// The root permalink is served by every language version that inherits it. A new root has no versions yet.
async function confirmRootPermalinkUsage(): Promise<boolean> {
  const inheritingCultures = languages.value
    .filter((language) => language.page && !isFieldOverridden(language.page.overriddenFields, "Permalink"))
    .map((language) => language.cultureName!);

  if (
    isNew.value ||
    !inheritingCultures.length ||
    !isPermalinkCheckNeeded({ permalink: savedSettings.value.permalink }, { permalink: item.value.permalink })
  ) {
    return true;
  }

  return confirmRootPermalinkUsageFor(inheritingCultures, item.value.permalink);
}

function confirmRootPermalinkUsageFor(cultureNames: string[], permalink: string | undefined): Promise<boolean> {
  return confirmPermalinkUsage({
    storeId: item.value.storeId,
    permalink,
    cultureNames,
    excludeMultilanguagePageId: item.value.id,
  });
}

/** Languages that override a field changed on the root, with those fields. */
function findConflicts(): IOverridesConflict[] {
  if (isNew.value) {
    return [];
  }

  const changed = getChangedFields(savedSettings.value, item.value);
  return languages.value
    .filter((language) => !!language.page)
    .map((language) => ({
      cultureName: language.cultureName!,
      fields: parseOverriddenFields(language.page?.overriddenFields).filter((field) => changed.includes(field)),
    }))
    .filter((conflict) => conflict.fields.length > 0);
}

async function saveRoot(resetFields: string[] = []) {
  const wasNew = isNew.value;
  const saved = await save();
  if (resetFields.length) {
    await resetOverrides(resetFields);
  }
  rememberSavedSettings();
  setBaseline();
  notification.success(t("PAGE_BUILDER.PAGES.ALERTS.SAVE_SUCCESS"));
  callParent("reload");
  if (wasNew && saved.id) {
    // Reopen with an id, so the language table and batch actions become available.
    callParent("onItemClick", { type: PageTypes.Multilanguage, id: saved.id });
  }
}

async function onApplyToAll() {
  const fields = [...new Set(conflicts.value.flatMap((conflict) => conflict.fields))];
  // Languages with their own permalink take the root one too; the save only checked the inheriting languages.
  const permalinkCultures = conflicts.value
    .filter((conflict) => conflict.fields.includes("Permalink"))
    .map((conflict) => conflict.cultureName);
  conflicts.value = [];
  if (permalinkCultures.length && !(await confirmRootPermalinkUsageFor(permalinkCultures, item.value.permalink))) {
    return;
  }
  await saveRoot(fields);
}

async function onApplyToInherited() {
  conflicts.value = [];
  await saveRoot();
}

function rememberSavedSettings() {
  const { permalink, visibility, userGroups, organizationId, startDate, endDate } = item.value;
  savedSettings.value = { permalink, visibility, userGroups, organizationId, startDate, endDate };
}

function notifyBatchResult(result: MultilanguagePageBatchResult, key: string) {
  const summary = summarizeBatchResult(result);
  notification.success(t(`PAGE_BUILDER.MULTILANGUAGE.ALERTS.${key}`, summary));
  if (summary.failed) {
    notification.error(t("PAGE_BUILDER.MULTILANGUAGE.ALERTS.BATCH_FAILED", { failed: summary.failed }));
  }
}

function openLanguage(language: MultilanguagePageLanguage) {
  openPage(language.page);
}

function openPage(page?: GroupedPageBuilderPage) {
  if (!page?.id) {
    return;
  }

  openBlade({
    name: "PageDetails",
    param: page.id,
    options: { storeId: item.value.storeId },
  });
}

function openDesigner(language: MultilanguagePageLanguage) {
  openPageDesigner({
    groupId: language.page?.id,
    storeId: language.page?.storeId,
    cultureName: language.page?.cultureName,
    status: language.page?.status,
  });
}

async function onCreateLanguage(language: MultilanguagePageLanguage) {
  // The new version inherits the saved root permalink in its language, not one still being edited.
  if (!(await confirmRootPermalinkUsageFor([language.cultureName!], savedSettings.value.permalink))) {
    return;
  }
  await createLanguage({ cultureName: language.cultureName!, copyContent: true });
  notification.success(t("PAGE_BUILDER.MULTILANGUAGE.ALERTS.LANGUAGE_CREATED", { language: language.cultureName }));
  callParent("reload");
}

async function onLink(groupId: string) {
  linkCultureName.value = undefined;
  await link(groupId);
  callParent("reload");
}

async function onUnlink(language: MultilanguagePageLanguage) {
  if (
    language.page?.id &&
    (await showConfirmation(t("PAGE_BUILDER.MULTILANGUAGE.ALERTS.UNLINK", { language: language.cultureName })))
  ) {
    if (!(await closeChildrenOrCancel())) {
      return;
    }
    await unlink(language.page.id);
    callParent("reload");
  }
}

async function onMarkUpToDate(language: MultilanguagePageLanguage) {
  if (language.page?.id) {
    await markUpToDate(language.page.id);
    callParent("reload");
  }
}

// The buttons repeat in every row, so a screen reader needs the language they act on.
function actionLabel(key: string, language: MultilanguagePageLanguage) {
  return t("PAGE_BUILDER.MULTILANGUAGE.LANGUAGES.ACTION_LABEL", { action: t(key), language: language.cultureName });
}

async function loadCultureNames() {
  const cultureNames = await getLanguages();
  return {
    totalCount: cultureNames.length,
    results: cultureNames.map((name) => ({ name })),
  };
}

// Language version blades opened from the table report back here.
exposeToChildren({
  reload: async () => {
    // The list first: the child may replace this blade right after (a clone opens from the list),
    // and a blade that is no longer in the stack cannot call its parent.
    callParent("reload");
    await loadLanguages();
  },
  // A version of this page reopens here; anything else (a clone, a converted page) belongs to the list.
  onItemClick: (page: GroupedPageBuilderPage & { type?: string }) =>
    page.type !== PageTypes.Multilanguage && page.multilanguagePageId === item.value.id
      ? openPage(page)
      : callParent("onItemClick", page),
});

onMounted(async () => {
  await load();
  // A new page starts with the store default language, the one the backend would pick on save.
  if (isNew.value && !item.value.defaultCultureName) {
    item.value.defaultCultureName = await getDefaultLanguage();
  }
  rememberSavedSettings();
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

.languages-table {
  @apply tw-w-full tw-text-sm;

  th {
    @apply tw-py-2 tw-pr-3 tw-text-left tw-font-normal;
    color: var(--neutrals-500);
    border-bottom: 1px solid var(--neutrals-200);
  }

  td {
    @apply tw-py-2 tw-pr-3 tw-align-middle;
    border-bottom: 1px solid var(--neutrals-100);
  }

  &__actions {
    @apply tw-flex tw-flex-wrap tw-gap-2;
  }

  // The link row fills the table width without widening it: a long page title would push the other rows' buttons out.
  &__link > td > * {
    width: 0;
    min-width: 100%;
  }
}
</style>
