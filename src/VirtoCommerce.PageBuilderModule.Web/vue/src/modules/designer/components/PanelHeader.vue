<template>
  <header class="pd-panel-head">
    <VcIcon
      :icon="icon"
      size="s"
      class="pd-panel-head__icon"
    />
    <h3 class="pd-panel-head__title">{{ title }}</h3>
    <slot />
    <VcButton
      variant="ghost"
      size="icon-sm"
      icon="lucide-x"
      :aria-label="closeLabel"
      :title="closeLabel"
      @click="emit('close')"
    />
  </header>
</template>

<script setup lang="ts">
import { VcButton, VcIcon } from "@vc-shell/framework/ui";

defineProps<{ icon: string; title: string; closeLabel: string }>();
const emit = defineEmits<{ close: [] }>();
</script>

<style lang="scss" scoped>
.pd-panel-head {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px 12px 12px 16px;
  border-bottom: 1px solid var(--neutrals-200);

  &__icon {
    color: var(--neutrals-500);
  }

  &__title {
    flex: 1;
    min-width: 0;
    margin: 0;
    font-size: 14px;
    font-weight: 600;
    color: var(--neutrals-900);
    overflow: hidden;
    white-space: nowrap;
    text-overflow: ellipsis;
  }
}
</style>
