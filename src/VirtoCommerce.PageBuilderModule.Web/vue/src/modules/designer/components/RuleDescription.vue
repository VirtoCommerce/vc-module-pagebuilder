<template>
  <template
    v-for="(part, i) in parts"
    :key="i"
  >
    <strong v-if="part.strong">{{ part.text }}</strong>
    <span
      v-else-if="part.unknown"
      class="pd-rules__badge"
      :title="$t('PAGE_BUILDER.DESIGNER.RULES.UNKNOWN_CONDITION', { name: part.unknown })"
      >{{ $t("PAGE_BUILDER.DESIGNER.RULES.UNKNOWN") }}</span
    >
    <template v-else>{{ part.text }}</template>
  </template>
</template>

<script setup lang="ts">
export interface RulePart {
  text: string;
  strong?: boolean;
  unknown?: string;
}

defineProps<{ parts: RulePart[] }>();
</script>
