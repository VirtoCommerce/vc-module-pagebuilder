<template>
  <VcRow>
    <VcCol>
      <VcLabel>{{ label }}</VcLabel>
      <VcHint>{{ hint }}</VcHint>
    </VcCol>

    <VcSwitch
      :model-value="modelValue"
      @update:model-value="emit('update:model-value', !!$event)"
    />
  </VcRow>
</template>
<script lang="ts" setup>
interface Props {
  label: string;
  hint: string;
  modelValue: boolean;
}

interface Emits {
  (event: "update:model-value", value: boolean): void;
}

defineProps<Props>();
const emit = defineEmits<Emits>();
</script>
