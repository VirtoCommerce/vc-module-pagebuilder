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
        <!-- Status -->
        <PageStatus
          :status="item.status"
          :has-changes="status.hasChanges"
        />

        <!-- Name Field -->
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

        <!-- Culture Name Field -->
        <VcSelect
          v-model="item.cultureName"
          :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.CULTURE_NAME')"
          :options="loadCultureNamesAsync"
          option-value="name"
          option-label="name"
          :clearable="false"
          :disabled="isReadOnly"
        />

        <!-- Permalink Field -->
        <VcInput
          v-model="item.permalink"
          :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.PERMALINK')"
          :disabled="isReadOnly"
        />

        <!-- Visibility Switch -->
        <VcSwitch
          v-model="item.visibility"
          :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.VISIBILITY')"
          :tooltip="$t('PAGE_BUILDER.PAGES.DETAILS.TOOLTIPS.VISIBILITY')"
          :disabled="isReadOnly"
        />

        <!-- User Groups -->
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

        <!-- User Groups -->
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

        <!-- Start Date -->
        <VcInput
          v-model="item.startDate"
          type="datetime-local"
          :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.START_DATE')"
          :disabled="isReadOnly"
        />

        <!-- End Date -->
        <VcInput
          v-model="item.endDate"
          type="datetime-local"
          :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.END_DATE')"
          :disabled="isReadOnly"
        />
      </VcForm>
    </VcContainer>
  </VcBlade>
</template>

<script lang="ts" setup>
import { computed, onMounted } from "vue";
import { useI18n } from "vue-i18n";
import { Field, useForm } from "vee-validate";
import { IBladeToolbar, IParentCallArgs, useBladeNavigation, usePopup } from "@vc-shell/framework";
import PageStatus from "../components/pageStatus.vue";
import { usePageBuilderDetails } from "../composables/usePageBuilderDetails";

defineOptions({
  name: "PageBuilderDetails",
  url: "/page-builder-details",
});

interface Props {
  expanded?: boolean;
  closable?: boolean;
  param?: string;
  options?: {
    storeId?: string;
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
  loadCultureNames,
  loadUserGroups,
  loadOrganizations,
} = usePageBuilderDetails({
  id: props.param,
  storeId: props.options?.storeId,
});

const bladeTitle = computed(() => {
  return !props.param
    ? item.value?.name
      ? item.value?.name + t("PAGE_BUILDER.PAGES.DETAILS.TITLE.DETAILS")
      : t("PAGE_BUILDER.PAGES.DETAILS.TITLE.NEW")
    : item.value?.name + t("PAGE_BUILDER.PAGES.DETAILS.TITLE.DETAILS");
});

function parseUserGroups(str: string | undefined): string[] {
  return str ? str.split(",").filter(x => !!x) : [];
}
function serializeUserGroups(groups: string[]): string {
  return groups.filter(x => !!x).join(",");
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
    disabled: !isModified.value || !meta.value.valid,
    clickHandler: async () => {
      await handleSave();
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
    id: "publishPage",
    icon: "material-description",
    title: t("PAGE_BUILDER.PAGES.DETAILS.TOOLBAR.PUBLISH"),
    isVisible: !!props.param && status.value?.hasChanges === true,
    disabled: isReadOnly.value,
    clickHandler: async () => {
      await publishGroup();
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
      emit("parent:call", { method: "reload" });
    },
  },
]);

// Methods
async function loadCultureNamesAsync() {
  return await loadCultureNames(props.options?.storeId);
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
