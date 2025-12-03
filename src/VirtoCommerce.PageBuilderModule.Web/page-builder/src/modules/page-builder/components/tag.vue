<template>
  <div class="tag-holder">
    {{ text }}
  </div>
</template>

<script lang="ts" setup>
interface Props {
  text: string;
}
defineProps<Props>();
</script>
<style>
.tag-holder {
  background-color: var(--status-primary-bg-color);
  color: var(--status-primary-main-color);
  border-color: var(--status-primary-main-color);
  @apply tw-inline-block tw-px-3 tw-text-xs tw-rounded-md tw-border;
}
</style>
