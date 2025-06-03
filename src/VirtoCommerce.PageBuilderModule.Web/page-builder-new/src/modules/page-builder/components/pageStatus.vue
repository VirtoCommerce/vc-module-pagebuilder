<template>
  <div style="display: flex; gap: 5px;">
    <VcStatus style="width: auto;"
      v-bind="statusStyles[itemStatus]"
      >{{ $t(`PAGE_BUILDER.STATUS.${itemStatus.toUpperCase()}`) }}</VcStatus
    >
    <template v-if="hasChanges && itemStatus == 'Published'">
      <VcStatus style="width: auto;"
        v-bind="statusStyles['HasChanges']"
        >{{ $t('PAGE_BUILDER.STATUS.HAS_CHANGES') }}</VcStatus
      >
    </template>
  </div>
</template>

<script lang="ts" setup>
import { useI18n } from "vue-i18n";
import { computed, toRefs } from "vue";

import {
  PageBuilderPage,
  GroupedPageBuilderPage,
} from "../../../api_client/virtocommerce.pagebuildermodule";

export interface Props {
  context: {
    item: GroupedPageBuilderPage;
  };
}

const { t } = useI18n();

const props = withDefaults(defineProps<Props>(), {
  
});

const { context } = toRefs(props);
const itemStatus = computed(() => getStatus(context.value.item) || "Draft");
const hasChanges = computed(() => context.value.item.hasChanges);

function getStatus(page: GroupedPageBuilderPage) {
  return page.status;
}

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
  }
};
</script>
