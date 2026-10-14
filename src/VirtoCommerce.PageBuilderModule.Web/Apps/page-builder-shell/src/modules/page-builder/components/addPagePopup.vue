<template>
  <VcPopup
    :model-value="true"
    :title="$t('PAGE_BUILDER.PAGES.ADD_POPUP.TITLE')"
    is-mobile-fullscreen
    @update:model-value="!$event && $emit('close')"
    @close="$emit('close')"
  >
    <template #content>
      <div class="add-page-popup">
        <button
          v-for="option in options"
          :key="option.type"
          type="button"
          class="add-page-popup__option"
          @click="$emit('select', option.type)"
        >
          <VcIcon :icon="option.icon" />
          <span class="add-page-popup__text">
            <span class="add-page-popup__title">{{ option.title }}</span>
            <VcHint>{{ option.description }}</VcHint>
          </span>
        </button>
      </div>
    </template>
  </VcPopup>
</template>

<script lang="ts" setup>
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { VcHint, VcIcon, VcPopup } from "@vc-shell/framework/ui";
import { PageTypes } from "../composables";

interface Emits {
  (event: "close"): void;
  (event: "select", type: PageTypes): void;
}

defineEmits<Emits>();

const { t } = useI18n({ useScope: "global" });

const options = computed(() => [
  {
    type: PageTypes.Multilanguage,
    icon: "lucide-languages",
    title: t("PAGE_BUILDER.PAGES.ADD_POPUP.MULTILANGUAGE.TITLE"),
    description: t("PAGE_BUILDER.PAGES.ADD_POPUP.MULTILANGUAGE.DESCRIPTION"),
  },
  {
    type: PageTypes.Landing,
    icon: "lucide-file-text",
    title: t("PAGE_BUILDER.PAGES.ADD_POPUP.LANDING.TITLE"),
    description: t("PAGE_BUILDER.PAGES.ADD_POPUP.LANDING.DESCRIPTION"),
  },
]);
</script>

<style lang="scss" scoped>
.add-page-popup {
  @apply tw-flex tw-flex-col tw-gap-3;

  &__option {
    @apply tw-flex tw-items-start tw-gap-3 tw-rounded-md tw-border tw-p-4 tw-text-left;
    border-color: var(--neutrals-200);

    &:hover,
    &:focus-visible {
      border-color: var(--primary-500);
      background-color: var(--primary-50);
    }
  }

  &__text {
    @apply tw-flex tw-flex-col tw-gap-1;
  }

  &__title {
    @apply tw-font-semibold;
  }
}
</style>
