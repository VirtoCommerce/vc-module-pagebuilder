<template>
  <div>
    <VcStatus
      v-bind="statusStyles[itemStatus]"
      >{{ $t(`PAGE_BUILDER.STATUS.${itemStatus.toUpperCase()}`) }}</VcStatus
    >
  </div>
</template>

<script lang="ts" setup>
import { computed, toRefs } from "vue";

import {
  PageBuilderPage,
} from "../../../api_client/virtocommerce.pagebuildermodule";

export interface Props {
  context: {
    item: PageBuilderPage;
  };
}

/*
const props = withDefaults(defineProps<Props>(), {
  context: () => ({
    item: {
      status: "Draft",
    },
  }),
});
*/

const props = withDefaults(defineProps<Props>(), {
  
});

const { context } = toRefs(props);
const itemStatus = computed(() => getStatus(context.value.item) || "Draft");

function getStatus(page: PageBuilderPage) {
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
};
</script>
