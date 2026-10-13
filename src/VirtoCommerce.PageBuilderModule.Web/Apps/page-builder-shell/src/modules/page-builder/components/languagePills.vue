<template>
  <div class="language-pills">
    <span
      v-for="language in languages"
      :key="language.cultureName"
      class="language-pills__pill"
      :class="`language-pills__pill--${variant(language)}`"
      :title="tooltip(language)"
    >
      {{ code(language.cultureName) }}
    </span>
  </div>
</template>

<script lang="ts" setup>
import { useI18n } from "vue-i18n";
import { MultilanguagePageLanguage } from "../../../api_client/virtocommerce.pagebuildermodule";

interface Props {
  languages: MultilanguagePageLanguage[];
}

defineProps<Props>();

const { t } = useI18n({ useScope: "global" });

function code(cultureName?: string): string {
  return (cultureName ?? "").split("-")[0].toUpperCase();
}

function variant(language: MultilanguagePageLanguage): string {
  if (language.status === "Published") {
    return language.hasChanges ? "changes" : "published";
  }

  return (language.status ?? "missing").toLowerCase();
}

function tooltip(language: MultilanguagePageLanguage): string {
  const status = t(`PAGE_BUILDER.STATUS.${(language.status ?? "Missing").toUpperCase()}`);
  return `${language.cultureName}: ${status}`;
}
</script>

<style lang="scss" scoped>
.language-pills {
  @apply tw-flex tw-flex-wrap tw-gap-1;

  &__pill {
    @apply tw-inline-block tw-rounded tw-border tw-px-1.5 tw-text-xs tw-font-medium;

    &--published {
      color: var(--success-700);
      background-color: var(--success-50);
      border-color: var(--success-200);
    }

    &--changes {
      color: var(--warning-700);
      background-color: var(--warning-50);
      border-color: var(--warning-200);
    }

    &--draft {
      color: var(--info-700);
      background-color: var(--info-50);
      border-color: var(--info-200);
    }

    &--archived,
    &--missing {
      color: var(--danger-700);
      background-color: var(--danger-50);
      border-color: var(--danger-200);
    }
  }
}
</style>
