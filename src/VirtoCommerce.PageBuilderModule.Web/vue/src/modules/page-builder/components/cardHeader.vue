<template>
  <VcRow class="tw-gap-2">
    <VcIcon :icon="icon" />
    <VcCol>
      <VcRow class="tw-gap-2 tw-items-center">
        <div class="tw-font-bold tw-text-sm">{{ title }}</div>
        <Tag
             v-if="!!tagText"
             :text="tagText" />
      </VcRow>
      <VcHint>{{ description }}</VcHint>
    </VcCol>
  </VcRow>
</template>
<script lang="ts" setup>
import { VcCol, VcHint, VcIcon, VcRow } from "@vc-shell/framework/ui";
import Tag from "./tag.vue";
interface Props {
  icon: string;
  title: string;
  tagText?: string;
  description: string;
}

defineProps<Props>();
</script>
