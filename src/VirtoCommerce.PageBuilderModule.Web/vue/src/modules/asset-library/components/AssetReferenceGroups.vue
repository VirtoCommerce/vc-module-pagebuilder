<template>
  <div
    v-for="group in groups"
    :key="group.title"
  >
    <p class="asset-references__title">
      {{ group.title }}
    </p>
    <ul
      class="asset-references__names"
      :aria-label="group.title"
    >
      <li
        v-for="name in group.names"
        :key="name"
      >
        {{ name }}
      </li>
    </ul>
  </div>
</template>

<script lang="ts" setup>
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import type { AssetReferenceDetails } from "../types";
import { getReferencePageNames, getReferenceSharedComponentNames } from "../utilities/assetReferences";

interface Props {
  references: AssetReferenceDetails;
}

const props = defineProps<Props>();
const { t } = useI18n({ useScope: "global" });

const groups = computed(() => {
  const { referencePages, referenceSharedComponents } = props.references;

  return [
    { title: t("ASSET_LIBRARY.DETAILS.PAGES"), names: getReferencePageNames(referencePages) },
    { title: t("ASSET_LIBRARY.DETAILS.SHARED_COMPONENTS"), names: getReferenceSharedComponentNames(referenceSharedComponents) },
  ].filter((group) => group.names.length);
});
</script>

<style lang="scss" scoped>
.asset-references {
  &__title {
    margin: 0 0 0.25rem;
    font-weight: 600;
  }

  &__names {
    max-height: 12rem;
    margin: 0;
    padding-inline-start: 1.25rem;
    overflow-y: auto;
    list-style: disc;
  }
}
</style>
