<template>
  <section class="git-card">
    <div class="git-card__head">
      <span class="git-card__icon">
        <VcIcon
          :icon="icon"
          size="m"
        />
      </span>
      <div class="git-card__title">
        <a
          v-if="href"
          class="git-card__name"
          :href="href"
          target="_blank"
          rel="noopener"
          >{{ title }}</a
        >
        <span
          v-else
          class="git-card__name"
          >{{ title }}</span
        >
        <span
          v-if="subtitle"
          class="git-card__subtitle"
          >{{ subtitle }}</span
        >
      </div>
      <VcStatus
        v-if="status"
        :variant="statusVariant ?? 'info'"
      >
        {{ status }}
      </VcStatus>
    </div>
    <dl
      v-if="$slots.default"
      class="git-card__rows"
    >
      <slot />
    </dl>
    <div
      v-if="$slots.actions"
      class="git-card__actions"
    >
      <slot name="actions" />
    </div>
  </section>
</template>

<script setup lang="ts">
// A connection at a glance, for the GitHub and History panels: what it is (linked), its state, its details as
// <div><dt/><dd/></div> rows, and what can be done with it.
import { VcIcon, VcStatus } from "@vc-shell/framework/ui";

defineProps<{
  icon: string;
  title: string;
  href?: string;
  subtitle?: string;
  status?: string;
  statusVariant?: "success" | "info" | "danger" | "warning";
}>();
</script>

<style lang="scss" scoped>
.git-card {
  border: 1px solid var(--neutrals-200);
  border-radius: 12px;
  overflow: hidden;

  &__head {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 16px;
  }

  &__icon {
    display: inline-flex;
    flex: none;
    align-items: center;
    justify-content: center;
    width: 40px;
    height: 40px;
    border-radius: 10px;
    background: var(--neutrals-100);
    color: var(--neutrals-800);
  }

  &__title {
    display: flex;
    flex: 1;
    flex-direction: column;
    min-width: 0;
  }

  &__name {
    overflow: hidden;
    white-space: nowrap;
    text-overflow: ellipsis;
    font-size: 15px;
    font-weight: 600;
    color: var(--neutrals-900);
  }

  a.git-card__name:hover {
    color: var(--link-text-color-hover);
  }

  &__subtitle {
    overflow: hidden;
    white-space: nowrap;
    text-overflow: ellipsis;
    font-size: 12px;
    color: var(--neutrals-500);
  }

  &__rows {
    margin: 0;
    border-top: 1px solid var(--neutrals-200);

    :deep(> div) {
      display: flex;
      align-items: baseline;
      justify-content: space-between;
      gap: 16px;
      padding: 10px 16px;

      & + div {
        border-top: 1px solid var(--neutrals-100);
      }
    }

    :deep(dt) {
      flex: none;
      color: var(--neutrals-500);
    }

    :deep(dd) {
      min-width: 0;
      margin: 0;
      overflow: hidden;
      white-space: nowrap;
      text-overflow: ellipsis;
      text-align: right;
      color: var(--neutrals-900);
    }
  }

  &__actions {
    display: flex;
    flex-wrap: wrap;
    gap: 8px;
    padding: 12px 16px;
    border-top: 1px solid var(--neutrals-200);
    background: var(--neutrals-50, transparent);
  }
}
</style>
