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
              rules="required"
              :model-value="item.permalink"
              :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.PERMALINK')"
            >
              <VcInput
                v-model="item.permalink"
                required
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
              <tr
                v-for="language in languages"
                :key="language.cultureName"
              >
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
                        isOverridden(language, 'Permalink')
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
                        @click="openLanguage(language)"
                      >
                        {{ $t("PAGE_BUILDER.MULTILANGUAGE.LANGUAGES.OPEN") }}
                      </VcButton>
                      <VcButton
                        size="sm"
                        variant="secondary"
                        :disabled="language.status === 'Archived'"
                        @click="openDesigner(language)"
                      >
                        {{ $t("PAGE_BUILDER.PAGES.DETAILS.TOOLBAR.DESIGNER") }}
                      </VcButton>
                      <VcButton
                        size="sm"
                        variant="secondary"
                        @click="onUnlink(language)"
                      >
                        {{ $t("PAGE_BUILDER.MULTILANGUAGE.LANGUAGES.UNLINK") }}
                      </VcButton>
                    </template>
                    <template v-else-if="language.isStoreLanguage">
                      <VcButton
                        size="sm"
                        @click="onCreateLanguage(language)"
                      >
                        {{ $t("PAGE_BUILDER.MULTILANGUAGE.LANGUAGES.CREATE") }}
                      </VcButton>
                      <VcButton
                        size="sm"
                        variant="secondary"
                        @click="linkCultureName = language.cultureName"
                      >
                        {{ $t("PAGE_BUILDER.MULTILANGUAGE.LANGUAGES.LINK") }}
                      </VcButton>
                    </template>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </VcCard>

        <PageAdvancedOptions
          v-model:item="item"
          :description="$t('PAGE_BUILDER.MULTILANGUAGE.FIELDS.ADVANCED_OPTIONS_HINT')"
        />
      </VcForm>
    </VcContainer>

    <LinkPagePopup
      v-if="linkCultureName"
      :culture-name="linkCultureName"
      :find-pages="findLandingPages"
      @close="linkCultureName = undefined"
      @link="onLink"
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
import LinkPagePopup from "../components/linkPagePopup.vue";
import Tag from "../components/tag.vue";

defineBlade({
  name: "MultilanguagePageDetails",
  url: "/multilanguage-details",
});

const { t } = useI18n({ useScope: "global" });
const { showConfirmation } = usePopup();
const { param, options, callParent, closeSelf, closeChildren, openBlade, exposeToChildren } = useBlade();
const { storeId, getStoreUrl, getLanguages } = useUrlParams();

const {
  item,
  languages,
  isNew,
  loading,
  load,
  save,
  remove,
  publishAll,
  archiveAll,
  createLanguage,
  link,
  unlink,
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
const linkCultureName = ref<string>();

const bladeTitle = computed(() =>
  isNew.value
    ? t("PAGE_BUILDER.MULTILANGUAGE.TITLE.NEW")
    : t("PAGE_BUILDER.MULTILANGUAGE.TITLE.DETAILS", { name: item.value.name }),
);

const hasVersions = computed(() => languages.value.some((x) => !!x.page));

const bladeToolbar = computed((): IBladeToolbar[] => [
  {
    id: "save",
    icon: "lucide-save",
    title: t("PAGE_BUILDER.PAGES.DETAILS.TOOLBAR.SAVE"),
    disabled: !canSave.value,
    clickHandler: async () => {
      const wasNew = isNew.value;
      const saved = await save();
      setBaseline();
      notification.success(t("PAGE_BUILDER.PAGES.ALERTS.SAVE_SUCCESS"));
      callParent("reload");
      if (wasNew && saved.id) {
        // Reopen with an id, so the language table and batch actions become available.
        callParent("onItemClick", { type: PageTypes.Multilanguage, id: saved.id });
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
      await closeChildren();
      notifyBatchResult(await publishAll(), "PUBLISH_ALL_RESULT");
      callParent("reload");
    },
  },
  {
    id: "archiveAll",
    icon: "lucide-archive",
    title: t("PAGE_BUILDER.MULTILANGUAGE.TOOLBAR.ARCHIVE_ALL"),
    isVisible: !isNew.value,
    disabled: !hasVersions.value,
    clickHandler: async () => {
      if (await showConfirmation(t("PAGE_BUILDER.MULTILANGUAGE.ALERTS.ARCHIVE_ALL"))) {
        await closeChildren();
        notifyBatchResult(await archiveAll(), "ARCHIVE_ALL_RESULT");
        callParent("reload");
      }
    },
  },
  {
    id: "delete",
    icon: "lucide-trash-2",
    title: t("PAGE_BUILDER.MULTILANGUAGE.TOOLBAR.DELETE"),
    isVisible: !isNew.value,
    clickHandler: async () => {
      if (await showConfirmation(t("PAGE_BUILDER.MULTILANGUAGE.ALERTS.DELETE"))) {
        await remove();
        callParent("reload");
        closeSelf();
      }
    },
  },
]);

function notifyBatchResult(result: MultilanguagePageBatchResult, key: string) {
  notification.success(
    t(`PAGE_BUILDER.MULTILANGUAGE.ALERTS.${key}`, {
      processed: result.processedCultureNames?.join(", ") || "—",
      skipped: result.skipped?.map((x) => x.cultureName).join(", ") || "—",
    }),
  );
}

function isOverridden(language: MultilanguagePageLanguage, field: string): boolean {
  return (language.page?.overriddenFields ?? "").split(",").includes(field);
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
    await closeChildren();
    await unlink(language.page.id);
    callParent("reload");
  }
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
    await load();
    callParent("reload");
  },
  onItemClick: (page: GroupedPageBuilderPage) => openPage(page),
});

onMounted(async () => {
  await load();
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
}
</style>
