<template>
  <aside
    class="tw-flex tw-w-full tw-shrink-0 tw-flex-col tw-border-0 tw-border-t tw-border-solid tw-border-t-[color:var(--neutrals-200)] tw-bg-[color:var(--additional-50)] tw-p-4 lg:tw-w-[420px] lg:tw-border-l lg:tw-border-t-0 lg:tw-border-l-[color:var(--neutrals-200)]"
    :aria-label="$t('LINKED_COMPONENTS.DETAILS.COMPONENT_DETAILS')"
    @click.stop
  >
    <div class="tw-flex tw-items-start tw-justify-between tw-gap-3">
      <div class="tw-flex tw-min-w-0 tw-items-start tw-justify-start tw-gap-3">
        <VcIcon
          icon="lucide-blocks"
          class="tw-shrink-0 tw-text-xl tw-text-[color:var(--primary-500)]"
        />
        <div class="tw-break-words tw-text-sm tw-font-semibold">
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

    <div class="tw-mt-5 tw-space-y-3">
      <div class="tw-text-xs tw-font-semibold tw-uppercase tw-text-[color:var(--neutrals-500)]">
        {{ $t("LINKED_COMPONENTS.DETAILS.COMPONENT_DETAILS") }}
      </div>

      <div class="tw-space-y-1 tw-break-words">
        <VcHint>{{ $t("LINKED_COMPONENTS.DETAILS.STORE") }}</VcHint>
        <div>{{ component.storeId }}</div>
      </div>

      <div class="tw-space-y-1 tw-break-words">
        <VcHint>{{ $t("LINKED_COMPONENTS.DETAILS.MODIFIED") }}</VcHint>
        <div>{{ formattedModifiedDate }}</div>
      </div>

      <div
        v-if="component.modifiedBy"
        class="tw-space-y-1 tw-break-words"
      >
        <VcHint>{{ $t("LINKED_COMPONENTS.DETAILS.MODIFIED_BY") }}</VcHint>
        <div>{{ component.modifiedBy }}</div>
      </div>
    </div>

    <div class="tw-mt-5 tw-min-h-0 tw-flex-1 tw-space-y-3">
      <div class="tw-text-xs tw-font-semibold tw-uppercase tw-text-[color:var(--neutrals-500)]">
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
        class="tw-max-h-[420px] tw-overflow-y-auto"
      >
        <button
          v-for="page in component.usagePages"
          :key="page.id || [page.permalink, page.cultureName, page.status].join('-')"
          type="button"
          class="tw-flex tw-w-full tw-cursor-pointer tw-items-start tw-gap-2 tw-border-0 tw-border-b tw-border-solid tw-border-b-[color:var(--neutrals-200)] tw-bg-transparent tw-px-1 tw-py-2 tw-text-left tw-text-sm tw-font-normal tw-text-inherit hover:tw-bg-[color:var(--neutrals-50)] focus-visible:tw-ring-2 focus-visible:tw-ring-inset focus-visible:tw-ring-[color:var(--primary-500)] disabled:tw-cursor-default disabled:hover:tw-bg-transparent"
          :disabled="!canOpenUsagePage(page)"
          @click="$emit('open-designer', page)"
        >
          <span
            class="tw-mt-1.5 tw-h-1.5 tw-w-1.5 tw-shrink-0 tw-rounded-full tw-bg-[color:var(--primary-500)]"
            aria-hidden="true"
          />
          <span class="tw-flex tw-min-w-0 tw-flex-1 tw-flex-col">
            <span class="tw-break-words tw-text-sm tw-font-medium">
              {{ getUsagePageTitle(page) }}
            </span>
            <span
              v-if="page.permalink"
              class="tw-break-words tw-text-xs tw-text-[color:var(--neutrals-500)]"
            >
              {{ page.permalink }}
            </span>
            <span
              v-if="page.cultureName || page.status"
              class="tw-break-words tw-text-xs tw-text-[color:var(--neutrals-500)]"
            >
              {{ [page.cultureName, page.status].filter(Boolean).join(" - ") }}
            </span>
            <span
              v-if="getUsagePageUnavailableReason(page)"
              class="tw-mt-1 tw-break-words tw-text-xs tw-italic tw-text-[color:var(--neutrals-500)]"
            >
              {{ getUsagePageUnavailableReason(page) }}
            </span>
          </span>
        </button>
      </div>
      <VcHint v-else>
        {{
          $t(
            component.usageCount > 0 ? "LINKED_COMPONENTS.DETAILS.NOT_AVAILABLE" : "LINKED_COMPONENTS.DETAILS.NO_USAGE",
          )
        }}
      </VcHint>
    </div>

    <div class="tw-mt-5 tw-flex tw-flex-col tw-gap-2">
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
        class="tw-text-[color:var(--danger-500)]"
        :disabled="loading || component.usageCount > 0"
        @click="$emit('delete', component)"
      >
        {{ $t("LINKED_COMPONENTS.ACTIONS.DELETE") }}
      </VcButton>

      <VcHint
        v-if="canDelete && component.usageCount > 0"
        class="tw-text-center tw-text-[color:var(--neutrals-500)]"
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
import type { LinkedComponent, LinkedComponentUsagePage } from "../types";
import { getUsagePageTitle } from "../utilities";

interface Props {
  component: LinkedComponent;
  canUpdate: boolean;
  canDelete: boolean;
  canOpenDesigner: boolean;
  loading: boolean;
  detailsLoading: boolean;
}

interface Emits {
  (event: "close"): void;
  (event: "rename", component: LinkedComponent): void;
  (event: "delete", component: LinkedComponent): void;
  (event: "open-designer", page: LinkedComponent["usagePages"][number]): void;
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

function canOpenUsagePage(page: LinkedComponentUsagePage): boolean {
  return props.canOpenDesigner && Boolean(page.id?.trim()) && page.status !== "Archived";
}

function getUsagePageUnavailableReason(page: LinkedComponentUsagePage): string | undefined {
  if (canOpenUsagePage(page)) {
    return undefined;
  }

  if (!page.id?.trim()) {
    return t("LINKED_COMPONENTS.DETAILS.OPEN_UNAVAILABLE_IDENTITY");
  }

  if (page.status === "Archived") {
    return t("LINKED_COMPONENTS.DETAILS.OPEN_UNAVAILABLE_ARCHIVED");
  }

  return t("LINKED_COMPONENTS.DETAILS.OPEN_UNAVAILABLE_PERMISSION");
}
</script>
