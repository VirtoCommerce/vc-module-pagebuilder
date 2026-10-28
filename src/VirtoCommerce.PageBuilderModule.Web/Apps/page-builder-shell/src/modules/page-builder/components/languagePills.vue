<template>
  <div class="language-pills">
    <span
      v-for="language in languages"
      :key="language.cultureName"
      class="language-pills__pill"
      :class="`language-pills__pill--${variant(language)}`"
      :title="tooltip(language)"
    >
      {{ text(language.cultureName) }}
    </span>
    <span
      v-if="withStatus && languages.length === 1"
      class="language-pills__status"
    >
      {{ statusText(languages[0]) }}
    </span>
  </div>
</template>

<script lang="ts" setup>
import { useI18n } from "vue-i18n";
import { MultilanguagePageLanguage } from "../../../api_client/virtocommerce.pagebuildermodule";
import { getLanguagePillText } from "../composables/useMultilanguagePageDetails/helpers";

interface Props {
  languages: MultilanguagePageLanguage[];
  /** Shows the status text next to a single pill. */
  withStatus?: boolean;
}

const props = defineProps<Props>();

const { t } = useI18n({ useScope: "global" });

// A page without a language is served in every language.
function text(cultureName?: string): string {
  return (
    getLanguagePillText(
      cultureName,
      props.languages.map((x) => x.cultureName),
    ) || t("PAGE_BUILDER.MULTILANGUAGE.LANGUAGES.ANY_LANGUAGE")
  );
}

function variant(language: MultilanguagePageLanguage): string {
  if (language.isOutdated) {
    return "outdated";
  }

  if (language.status === "Published") {
    return language.hasChanges ? "changes" : "published";
  }

  return (language.status ?? "missing").toLowerCase();
}

function statusText(language: MultilanguagePageLanguage): string {
  const status = t(`PAGE_BUILDER.STATUS.${(language.status ?? "Missing").toUpperCase()}`);
  return language.isOutdated ? `${status}, ${t("PAGE_BUILDER.STATUS.OUTDATED")}` : status;
}

function tooltip(language: MultilanguagePageLanguage): string {
  return `${language.cultureName || t("PAGE_BUILDER.MULTILANGUAGE.LANGUAGES.ANY_LANGUAGE")}: ${statusText(language)}`;
}
</script>

<style lang="scss" scoped>
.language-pills {
  @apply tw-flex tw-flex-wrap tw-items-center tw-gap-1;

  &__pill {
    @apply tw-inline-block tw-rounded tw-border tw-px-1.5 tw-text-xs tw-font-medium;

    &--published {
      color: var(--success-700);
      background-color: var(--success-50);
      border-color: var(--success-200);
    }

    &--outdated {
      color: var(--warning-700);
      background-color: var(--warning-50);
      border-color: var(--warning-500);
      border-style: dashed;
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

    &--archived {
      color: var(--danger-700);
      background-color: var(--danger-50);
      border-color: var(--danger-200);
    }

    // No version yet: not an error, just absent.
    &--missing {
      color: var(--neutrals-500);
      border-style: dashed;
      border-color: var(--neutrals-300);
    }
  }

  &__status {
    @apply tw-text-xs;
    color: var(--neutrals-600);
  }
}
</style>
