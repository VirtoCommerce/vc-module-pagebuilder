<template>
  <div class="tw-flex tw-flex-row tw-gap-1">
    <VcStatus
      v-if="item.status"
      class="w-auto"
      v-bind="statusStyles[item.status]"
      >{{ $t(`PAGE_BUILDER.STATUS.${item.status.toUpperCase()}`) }}</VcStatus
    >

    <template v-if="item.hasChanges && item.status == PageStatuses.Published">
      <VcStatus
        class="w-auto"
        v-bind="statusStyles['HasChanges']"
        >{{ $t("PAGE_BUILDER.STATUS.HAS_CHANGES") }}</VcStatus
      >
    </template>

    <template v-if="extended">
      <template v-if="item.startDate || item.endDate">
        <VcStatus
          class="w-auto"
          v-bind="statusStyles['Scheduled']"
          >{{ $t("PAGE_BUILDER.STATUS.SCHEDULED") }}</VcStatus
        >
      </template>

      <template v-if="item.userGroups">
        <VcStatus
          v-for="value in item.userGroups.split(',')"
          :key="value"
          class="w-auto"
          v-bind="statusStyles['Access']"
          >{{ value }}</VcStatus
        >
      </template>

      <template v-if="!item.visibility">
        <VcStatus
          class="w-auto"
          v-bind="statusStyles['Access']"
          >{{ $t("PAGE_BUILDER.STATUS.REGISTERED_ONLY") }}</VcStatus
        >
      </template>

      <template v-if="item.organizationId">
        <VcStatus
          class="w-auto"
          v-bind="statusStyles['Access']"
          >{{ organization }}</VcStatus
        >
      </template>
    </template>
  </div>
</template>

<script lang="ts" setup>
import { onMounted, ref } from "vue";
import { GroupedPageBuilderPage } from "../../../api_client/virtocommerce.pagebuildermodule";
import { PageStatuses } from "../composables";
import useOrganizations from "../composables/useOrganizations";

export interface Props {
  item: GroupedPageBuilderPage;
  extended?: boolean;
}

const props = defineProps<Props>();

const organization = ref<string | null>(null);
const { getOrganization } = useOrganizations();

const statusStyles: Omit<Record<string, Record<string, unknown>>, "Draft"> = {
  Draft: {
    outline: true,
    variant: "info",
  },
  Archived: {
    outline: true,
    variant: "danger",
  },
  Published: {
    outline: true,
    variant: "success",
  },
  HasChanges: {
    outline: true,
    variant: "warning",
  },
  Scheduled: {
    outline: true,
    variant: "light-danger",
  },
  Access: {
    outline: true,
    variant: "info-dark",
  },
};

onMounted(async () => {
  if (props.item.organizationId) {
    organization.value = await getOrganization(props.item.organizationId);
  }
});
</script>
