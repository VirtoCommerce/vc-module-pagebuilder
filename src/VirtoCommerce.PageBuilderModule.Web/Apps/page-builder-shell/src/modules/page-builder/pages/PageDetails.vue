<template>
  <VcBlade
    v-loading="loading"
    :title="bladeTitle"
    width="50%"
    :expanded="expanded"
    :closable="closable"
    :toolbar-items="bladeToolbar"
    :modified="isModified"
    @close="$emit('close:blade')"
    @expand="$emit('expand:blade')"
    @collapse="$emit('collapse:blade')"
  >
    <VcContainer>
      <VcForm class="tw-flex tw-flex-col tw-gap-4">
        <PageStatus :item="item" />

        <VcCard
          header="Basic information"
          class="tw-p-4"
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
                :disabled="isReadOnly"
                :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.NAME')"
                required
                @update:model-value="handleChange"
              />
            </Field>

            <Field
              v-slot="{ errorMessage, handleChange, errors }"
              name="permalink"
              :model-value="item.permalink"
              :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.PERMALINK')"
              rules="required"
            >
              <VcInput
                v-model="item.permalink"
                :error="errors.length > 0"
                :error-message="errorMessage"
                required
                :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.PERMALINK')"
                :disabled="isReadOnly"
                @update:model-value="handleChange"
              >
                <template #prepend-inner>
                  <div
                    v-if="storeUrl"
                    class="-tw-ml-2.5 tw-p-2 tw-text-sm tw-rounded-sm tw-pl-2.5 tw-bg-gray-300"
                  >
                    {{ storeUrl }}
                  </div>
                </template>
              </VcInput>
            </Field>

            <VcSelect
              v-model="item.cultureName"
              :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.CULTURE_NAME')"
              :options="loadCultureNamesAsync"
              option-value="name"
              option-label="name"
              :clearable="false"
              :disabled="isReadOnly"
            />
          </VcCol>
        </VcCard>

        <VcCard
          header="Advanced options"
          class="tw-p-4"
        >
          <VcCol class="tw-gap-4">
            <VcCard
              header="header"
              class="tw-p-4"
              is-collapsable
              is-collapsed
            >
              <template #header>
                <CardHeader
                  icon="material-group"
                  :tag-text="
                    item.visibility
                      ? $t('PAGE_BUILDER.PAGES.DETAILS.SECTIONS.PERSONALIZATION_ACCESS_CONTROL.VISIBILITY_TAG')
                      : undefined
                  "
                  :title="$t('PAGE_BUILDER.PAGES.DETAILS.SECTIONS.PERSONALIZATION_ACCESS_CONTROL.TITLE')"
                  :description="$t('PAGE_BUILDER.PAGES.DETAILS.SECTIONS.PERSONALIZATION_ACCESS_CONTROL.DESCRIPTION')"
                />
              </template>

              <VcCol class="tw-gap-4">
                <SwitchRow
                  :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.VISIBILITY')"
                  :hint="$t('PAGE_BUILDER.PAGES.DETAILS.TOOLTIPS.VISIBILITY')"
                  :model-value="item.visibility"
                  @update:model-value="item.visibility = $event"
                />

                <VcSelect
                  v-model="itemUserGroups"
                  :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.USER_GROUPS')"
                  :options="loadUserGroups"
                  option-value="name"
                  option-label="name"
                  searchable
                  multiple
                  :clearable="false"
                  :disabled="isReadOnly"
                />

                <VcSelect
                  v-model="item.organizationId"
                  :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.ORGANIZATION')"
                  :options="loadOrganizations"
                  option-value="id"
                  option-label="name"
                  searchable
                  :clearable="true"
                  :disabled="isReadOnly"
                />
              </VcCol>
            </VcCard>

            <VcCard
              header="header"
              is-collapsable
              is-collapsed
              class="tw-p-4"
            >
              <template #header>
                <CardHeader
                  icon="material-date_range"
                  :tag-text="isScheduled ? $t('PAGE_BUILDER.PAGES.DETAILS.SECTIONS.SCHEDULING.TAG_TEXT') : ''"
                  :title="$t('PAGE_BUILDER.PAGES.DETAILS.SECTIONS.SCHEDULING.TITLE')"
                  :description="$t('PAGE_BUILDER.PAGES.DETAILS.SECTIONS.SCHEDULING.DESCRIPTION')"
                />
              </template>
              <VcRow class="tw-gap-4">
                <VcInput
                  v-model="item.startDate"
                  class="tw-flex-1"
                  type="datetime-local"
                  clearable
                  :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.START_DATE')"
                  :hint="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.START_DATE_HINT')"
                  :disabled="isReadOnly"
                />

                <VcInput
                  v-model="item.endDate"
                  class="tw-flex-1"
                  type="datetime-local"
                  clearable
                  :hint="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.END_DATE_HINT')"
                  :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.END_DATE')"
                  :disabled="isReadOnly"
                />
              </VcRow>
            </VcCard>
          </VcCol>
        </VcCard>
      </VcForm>
    </VcContainer>
  </VcBlade>
</template>

<script lang="ts" setup>
import { computed, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { Field, useForm } from "vee-validate";
import { IBladeToolbar, IParentCallArgs, useBladeNavigation, usePopup, notification } from "@vc-shell/framework";
import PageStatus from "../components/pageStatus.vue";
import useUrlParams from "../composables/useStoreParams";
import { usePageBuilderDetails } from "../composables/usePageBuilderDetails";
import type { PageExportData } from "../composables/usePageContentApi";

defineOptions({
  name: "PageDetails",
  url: "/details",
});

interface Props {
  expanded?: boolean;
  closable?: boolean;
  param?: string;
  options?: {
    storeId?: string;
    importData?: PageExportData;
  };
}

interface Emits {
  (event: "close:blade"): void;
  (event: "expand:blade"): void;
  (event: "collapse:blade"): void;
  (event: "parent:call", args: IParentCallArgs): void;
}

const props = withDefaults(defineProps<Props>(), {
  expanded: true,
  closable: true,
});

const emit = defineEmits<Emits>();

const { t } = useI18n({ useScope: "global" });
const { showConfirmation } = usePopup();
const { onBeforeClose } = useBladeNavigation();
const { meta } = useForm({ validateOnMount: false });
const { getStoreUrl, getLanguages } = useUrlParams();

const {
  item,
  status,
  isModified,
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
  id: props.param,
  storeId: props.options?.storeId,
  importData: props.options?.importData,
});

const bladeTitle = computed(() => {
  if (props.param || item.value?.name) {
    return item.value?.name + t("PAGE_BUILDER.PAGES.DETAILS.TITLE.DETAILS");
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
    icon: "material-save",
    title: t("PAGE_BUILDER.PAGES.DETAILS.TOOLBAR.SAVE"),
    disabled: !isModified.value || isReadOnly.value || !meta.value.valid,
    clickHandler: async () => {
      await handleSave();
      notification.success(t("PAGE_BUILDER.PAGES.ALERTS.SAVE_SUCCESS"));
    },
  },
  {
    id: "delete",
    icon: "material-delete",
    title: t("PAGE_BUILDER.PAGES.DETAILS.TOOLBAR.DELETE"),
    disabled: isReadOnly.value,
    clickHandler: async () => {
      if (await showConfirmation(t("PAGE_BUILDER.PAGES.ALERTS.DELETE"))) {
        await deleteGroup();
        notification.success(t("PAGE_BUILDER.PAGES.ALERTS.DELETE_SUCCESS"));
        emit("parent:call", { method: "reload" });
        emit("close:blade");
      }
    },
  },
  {
    id: "openPageDesigner",
    icon: "material-crop",
    title: t("PAGE_BUILDER.PAGES.DETAILS.TOOLBAR.DESIGNER"),
    disabled: !props.param || isReadOnly.value,
    clickHandler: () => {
      openDraftDesigner();
    },
  },
  {
    id: "downloadContent",
    icon: "material-download",
    title: t("PAGE_BUILDER.PAGES.DETAILS.TOOLBAR.DOWNLOAD_CONTENT"),
    disabled: !props.param,
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
    icon: "material-content_copy",
    title: t("PAGE_BUILDER.PAGES.DETAILS.TOOLBAR.CLONE"),
    disabled: !props.param || isReadOnly.value || loading.value,
    clickHandler: async () => {
      if (loading.value) return;
      const cloned = await clonePage();
      if (cloned?.id) {
        notification.success(t("PAGE_BUILDER.PAGES.ALERTS.CLONE_SUCCESS"));
        emit("parent:call", { method: "reload" });
        emit("parent:call", { method: "onItemClick", args: cloned });
      }
    },
  },
  {
    id: "publishPage",
    icon: "material-description",
    title: t("PAGE_BUILDER.PAGES.DETAILS.TOOLBAR.PUBLISH"),
    isVisible: !!props.param && status.value?.hasChanges === true,
    disabled: isReadOnly.value || !meta.value.valid || isModified.value,
    clickHandler: async () => {
      await publishGroup();
      notification.success(t("PAGE_BUILDER.PAGES.ALERTS.PUBLISH_SUCCESS"));
      emit("parent:call", { method: "reload" });
    },
  },
  {
    id: "unpublishPage",
    icon: "material-article",
    title: t("PAGE_BUILDER.PAGES.DETAILS.TOOLBAR.UNPUBLISH"),
    isVisible: !!props.param && status.value?.hasChanges === false,
    disabled: isReadOnly.value,
    clickHandler: async () => {
      await unpublishGroup();
      notification.success(t("PAGE_BUILDER.PAGES.ALERTS.UNPUBLISH_SUCCESS"));
      emit("parent:call", { method: "reload" });
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

  emit("parent:call", { method: "reload" });

  if (item.value.id || group.id) {
    emit("parent:call", { method: "onItemClick", args: group.id ? group : item.value });
  }
}

// Lifecycle
onMounted(async () => {
  await loadGroup();
  storeUrl.value = await getStoreUrl();
});

onBeforeClose(async () => {
  if (isModified.value) {
    return showConfirmation(t("PAGE_BUILDER.PAGES.ALERTS.CLOSE_CONFIRMATION"));
  }
  return true;
});

defineExpose({
  title: bladeTitle,
});
</script>
