<template>
  <aside
    class="linked-components__details"
    @click.stop
  >
    <div class="linked-components__details-header">
      <div class="linked-components__details-heading">
        <VcIcon
          icon="lucide-blocks"
          class="linked-components__details-icon"
        />
        <div class="linked-components__details-title">
          {{ component.name }}
        </div>
      </div>

      <VcButton
        icon="lucide-x"
        text
        :aria-label="$t('LINKED_COMPONENTS.ACTIONS.CLOSE')"
        @click="$emit('close')"
      />
    </div>

    <div class="linked-components__details-section">
      <div class="linked-components__section-title">
        {{ $t("LINKED_COMPONENTS.DETAILS.COMPONENT_DETAILS") }}
      </div>

      <div class="linked-components__detail-row">
        <VcHint>{{ $t("LINKED_COMPONENTS.DETAILS.STORE") }}</VcHint>
        <div>{{ component.storeId }}</div>
      </div>

      <div class="linked-components__detail-row">
        <VcHint>{{ $t("LINKED_COMPONENTS.DETAILS.MODIFIED") }}</VcHint>
        <div>{{ formattedModifiedDate }}</div>
      </div>

      <div
        v-if="component.modifiedBy"
        class="linked-components__detail-row"
      >
        <VcHint>{{ $t("LINKED_COMPONENTS.DETAILS.MODIFIED_BY") }}</VcHint>
        <div>{{ component.modifiedBy }}</div>
      </div>
    </div>

    <div class="linked-components__details-section linked-components__details-section--usage">
      <div class="linked-components__section-title">
        {{ $t(usedOnTranslationKey, { count: component.usageCount }) }}
      </div>

      <VcSkeleton
        v-if="detailsLoading"
        variant="text"
        :rows="3"
        :aria-label="$t('LINKED_COMPONENTS.DETAILS.LOADING_USAGE')"
      />
      <div
        v-else-if="component.usagePages.length"
        class="linked-components__usage-list"
      >
        <button
          v-for="page in component.usagePages"
          :key="page.id || [page.permalink, page.cultureName, page.status].join('-')"
          type="button"
          class="linked-components__usage-page"
          :disabled="!canOpenPages || !page.id"
          @click="$emit('open-page', page)"
        >
          <span
            class="linked-components__usage-page-dot"
            aria-hidden="true"
          />
          <span class="linked-components__usage-page-content">
            <span class="linked-components__usage-page-title">
              {{ getUsagePageTitle(page) }}
            </span>
            <span
              v-if="page.permalink"
              class="linked-components__usage-page-meta"
            >
              {{ page.permalink }}
            </span>
            <span
              v-if="page.cultureName || page.status"
              class="linked-components__usage-page-meta"
            >
              {{ [page.cultureName, page.status].filter(Boolean).join(" - ") }}
            </span>
          </span>
        </button>
      </div>
      <VcHint v-else>
        {{ $t("LINKED_COMPONENTS.DETAILS.NO_USAGE") }}
      </VcHint>
    </div>

    <div class="linked-components__details-actions">
      <VcButton
        v-if="canUpdate"
        variant="secondary"
        icon="lucide-pencil"
        :disabled="loading"
        @click="$emit('rename', component)"
      >
        {{ $t("LINKED_COMPONENTS.ACTIONS.RENAME") }}
      </VcButton>

      <VcButton
        v-if="canDelete"
        variant="secondary"
        icon="lucide-trash-2"
        class="linked-components__delete-button"
        :disabled="loading || component.usageCount > 0"
        @click="$emit('delete', component)"
      >
        {{ $t("LINKED_COMPONENTS.ACTIONS.DELETE") }}
      </VcButton>

      <VcHint
        v-if="canDelete && component.usageCount > 0"
        class="linked-components__delete-hint"
      >
        {{ $t(deleteBlockedTranslationKey, { count: component.usageCount }) }}
      </VcHint>
    </div>
  </aside>
</template>

<script lang="ts" setup>
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { VcButton, VcHint, VcIcon, VcSkeleton } from "@vc-shell/framework/ui";
import type { LinkedComponent } from "../types";
import { getUsagePageTitle } from "../utilities";

interface Props {
  component: LinkedComponent;
  canUpdate: boolean;
  canDelete: boolean;
  canOpenPages: boolean;
  loading: boolean;
  detailsLoading: boolean;
}

interface Emits {
  (event: "close"): void;
  (event: "rename", component: LinkedComponent): void;
  (event: "delete", component: LinkedComponent): void;
  (event: "open-page", page: LinkedComponent["usagePages"][number]): void;
}

const props = defineProps<Props>();
defineEmits<Emits>();

const { t, locale } = useI18n({ useScope: "global" });
const usedOnTranslationKey = computed(() =>
  props.component.usageCount === 1 ? "LINKED_COMPONENTS.DETAILS.USED_ON_ONE" : "LINKED_COMPONENTS.DETAILS.USED_ON_MANY",
);
const deleteBlockedTranslationKey = computed(() =>
  props.component.usageCount === 1
    ? "LINKED_COMPONENTS.DETAILS.DELETE_BLOCKED_ONE"
    : "LINKED_COMPONENTS.DETAILS.DELETE_BLOCKED_MANY",
);
const formattedModifiedDate = computed(() => {
  const value = props.component.modifiedDate;

  if (!value) {
    return t("LINKED_COMPONENTS.DETAILS.NOT_AVAILABLE");
  }

  const date = value instanceof Date ? value : new Date(value);

  return Number.isNaN(date.getTime())
    ? t("LINKED_COMPONENTS.DETAILS.NOT_AVAILABLE")
    : new Intl.DateTimeFormat(locale.value, { dateStyle: "medium", timeStyle: "short" }).format(date);
});
</script>

<style lang="scss" scoped>
.linked-components {
  &__details {
    @apply tw-flex tw-w-full tw-shrink-0 tw-flex-col tw-border-t tw-border-solid tw-border-t-[color:var(--neutrals-200)] tw-bg-[color:var(--additional-50)] tw-p-4 lg:tw-w-[420px] lg:tw-border-l lg:tw-border-t-0 lg:tw-border-l-[color:var(--neutrals-200)];
  }

  &__details-header,
  &__details-heading {
    @apply tw-flex tw-items-start tw-justify-between tw-gap-3;
  }

  &__details-heading {
    @apply tw-min-w-0 tw-justify-start;
  }

  &__details-icon {
    @apply tw-shrink-0 tw-text-xl tw-text-[color:var(--primary-500)];
  }

  &__details-title {
    @apply tw-break-words tw-text-sm tw-font-semibold tw-leading-[18px];
  }

  &__details-section {
    @apply tw-mt-5 tw-space-y-3;
  }

  &__details-section--usage {
    @apply tw-min-h-0 tw-flex-1;
  }

  &__section-title {
    @apply tw-text-xs tw-font-semibold tw-uppercase tw-text-[color:var(--neutrals-500)];
  }

  &__detail-row {
    @apply tw-space-y-1 tw-break-words;
  }

  &__usage-list {
    @apply tw-max-h-[420px] tw-overflow-y-auto;
  }

  &__usage-page {
    @apply tw-flex tw-w-full tw-cursor-pointer tw-items-start tw-gap-2 tw-bg-transparent tw-px-1 tw-py-2 tw-text-left tw-text-inherit;
    border: 0;
    border-bottom: 1px solid var(--neutrals-200);
    font: inherit;
    transition:
      border-color 0.16s ease,
      background-color 0.16s ease;

    &:hover:not(:disabled),
    &:focus-visible:not(:disabled) {
      @apply tw-bg-[color:var(--neutrals-50)];
    }

    &:focus-visible:not(:disabled) {
      outline: 2px solid var(--primary-500);
      outline-offset: -2px;
    }

    &:disabled {
      @apply tw-cursor-default;
    }
  }

  &__usage-page-dot {
    @apply tw-mt-[6px] tw-h-1.5 tw-w-1.5 tw-shrink-0 tw-rounded-full tw-bg-[color:var(--primary-500)];
  }

  &__usage-page-content {
    @apply tw-flex tw-min-w-0 tw-flex-1 tw-flex-col;
  }

  &__usage-page-title {
    @apply tw-break-words tw-text-sm tw-font-medium;
  }

  &__usage-page-meta {
    @apply tw-break-words tw-text-xs tw-text-[color:var(--neutrals-500)];
  }

  &__details-actions {
    @apply tw-mt-5 tw-flex tw-flex-col tw-gap-2;
  }

  &__delete-button {
    color: var(--danger-500);
  }

  &__delete-hint {
    @apply tw-text-center tw-text-[color:var(--neutrals-500)];
  }
}
</style>
