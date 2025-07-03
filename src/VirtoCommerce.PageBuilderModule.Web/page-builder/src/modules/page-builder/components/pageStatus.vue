<template>
  <div class="flex gap-1">
    <VcStatus
      class="w-auto"
      v-bind="statusStyles[status]"
      >{{ $t(`PAGE_BUILDER.STATUS.${status.toUpperCase()}`) }}</VcStatus
    >
    <template v-if="hasChanges && status == 'Published'">
      <VcStatus
        class="w-auto"
        v-bind="statusStyles['HasChanges']"
        >{{ $t("PAGE_BUILDER.STATUS.HAS_CHANGES") }}</VcStatus
      >
    </template>
  </div>
</template>

<script lang="ts" setup>
export interface Props {
  status?: string;
  hasChanges?: boolean;
}

withDefaults(defineProps<Props>(), {
  status: "Draft",
});

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
};
</script>
