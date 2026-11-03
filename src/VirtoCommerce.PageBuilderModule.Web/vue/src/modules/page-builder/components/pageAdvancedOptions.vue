<template>
  <VcCard
    class="tw-p-4"
    :header="$t('PAGE_BUILDER.PAGES.DETAILS.SECTIONS.ADVANCED_OPTIONS')"
  >
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
            :description="
              description ?? $t('PAGE_BUILDER.PAGES.DETAILS.SECTIONS.PERSONALIZATION_ACCESS_CONTROL.DESCRIPTION')
            "
          />
        </template>

        <VcCol class="tw-gap-4 tw-p-4">
          <SwitchRow
            :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.VISIBILITY')"
            :hint="withFieldHint($t('PAGE_BUILDER.PAGES.DETAILS.TOOLTIPS.VISIBILITY'), 'Visibility')"
            :model-value="item.visibility"
            @update:model-value="item.visibility = $event"
          />

          <VcSelect
            v-model="userGroups"
            :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.USER_GROUPS')"
            :hint="fieldHints?.UserGroups"
            :options="getUserGroups"
            option-value="name"
            option-label="name"
            searchable
            multiple
            :clearable="false"
            :disabled="disabled"
          />

          <VcSelect
            v-model="item.organizationId"
            :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.ORGANIZATION')"
            :hint="fieldHints?.OrganizationId"
            :options="getOrganizations"
            option-value="id"
            option-label="name"
            searchable
            :clearable="true"
            :disabled="disabled"
          />
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
            :hint="withFieldHint($t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.START_DATE_HINT'), 'StartDate')"
            :disabled="disabled"
          />

          <VcInput
            v-model="item.endDate"
            class="tw-flex-1"
            type="datetime-local"
            clearable
            :hint="withFieldHint($t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.END_DATE_HINT'), 'EndDate')"
            :label="$t('PAGE_BUILDER.PAGES.DETAILS.FIELDS.END_DATE')"
            :disabled="disabled"
          />
        </VcRow>
      </VcCard>
    </VcCol>
  </VcCard>
</template>

<script lang="ts" setup>
import { computed } from "vue";
import { VcCard, VcCol, VcInput, VcRow, VcSelect } from "@vc-shell/framework/ui";
import useUserGroups from "../composables/useUserGroups";
import useOrganizations from "../composables/useOrganizations";
import CardHeader from "./cardHeader.vue";
import SwitchRow from "./switchRow.vue";
import type { InheritableField } from "../composables/useMultilanguageInheritance";

/** Settings shared by Landing Pages, Multilanguage Pages and their language versions. */
export interface PageAdvancedOptionsModel {
  visibility?: boolean;
  userGroups?: string;
  organizationId?: string;
  startDate?: Date;
  endDate?: Date;
}

interface Props {
  disabled?: boolean;
  /** Overrides the Personalization & Access control description. */
  description?: string;
  /** Language versions: whether each field is inherited from the Multilanguage Page or set for the language. */
  fieldHints?: Partial<Record<InheritableField, string>>;
}

const props = defineProps<Props>();

function withFieldHint(hint: string, field: InheritableField): string {
  const fieldHint = props.fieldHints?.[field];
  return fieldHint ? `${hint} · ${fieldHint}` : hint;
}

const item = defineModel<PageAdvancedOptionsModel>("item", { required: true });

const { getUserGroups } = useUserGroups();
const { getOrganizations } = useOrganizations();

const isScheduled = computed(() => !!item.value.startDate || !!item.value.endDate);

const userGroups = computed<string[]>({
  get: () => (item.value.userGroups ? item.value.userGroups.split(",").filter((x) => !!x) : []),
  set: (value: string[]) => {
    item.value.userGroups = value.filter((x) => !!x).join(",");
  },
});
</script>
