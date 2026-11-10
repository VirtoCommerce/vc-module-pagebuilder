<template>
  <VcPopup
    :model-value="true"
    variant="warning"
    :title="$t('ASSET_LIBRARY.CONFIRM.TITLE')"
    modal-width="tw-max-w-xl"
    @close="$emit('cancel')"
  >
    <template #content>
      <div class="asset-delete">
        <p class="asset-delete__message">
          {{ message }}
        </p>

        <div
          v-for="group in referenceGroups"
          :key="group.title"
          class="asset-delete__group"
        >
          <p class="asset-delete__group-title">
            {{ group.title }}
          </p>
          <ul
            class="asset-delete__names"
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
      </div>
    </template>

    <template #footer>
      <div class="tw-flex tw-w-full tw-flex-wrap tw-justify-end tw-gap-3">
        <VcButton
          variant="secondary"
          @click="$emit('cancel')"
        >
          {{ $t("ASSET_LIBRARY.CONFIRM.CANCEL") }}
        </VcButton>
        <VcButton
          variant="danger"
          @click="$emit('confirm')"
        >
          {{ $t("ASSET_LIBRARY.CONFIRM.DELETE") }}
        </VcButton>
      </div>
    </template>
  </VcPopup>
</template>

<script lang="ts" setup>
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { VcButton, VcPopup } from "@vc-shell/framework/ui";
import type { AssetDeleteConfirmation } from "../composables/useAssetLibraryActions";
import { getReferencePageNames, getReferenceSharedComponentNames } from "../utilities/assetReferences";

interface Props {
  confirmation: AssetDeleteConfirmation;
}

interface Emits {
  (event: "cancel"): void;
  (event: "confirm"): void;
}

const props = defineProps<Props>();
defineEmits<Emits>();
const { t } = useI18n({ useScope: "global" });

const message = computed(() => {
  const { entry, references } = props.confirmation;
  const count = references.referencesCount;

  if (!count) {
    return t("ASSET_LIBRARY.CONFIRM.DELETE_SINGLE", { name: entry.name });
  }

  const key = count === 1 ? "ASSET_LIBRARY.CONFIRM.DELETE_USED_ONE" : "ASSET_LIBRARY.CONFIRM.DELETE_USED_MANY";
  return t(key, { name: entry.name, count });
});
const referenceGroups = computed(() => {
  const { referencePages, referenceSharedComponents } = props.confirmation.references;

  return [
    { title: t("ASSET_LIBRARY.DETAILS.PAGES"), names: getReferencePageNames(referencePages) },
    { title: t("ASSET_LIBRARY.DETAILS.SHARED_COMPONENTS"), names: getReferenceSharedComponentNames(referenceSharedComponents) },
  ].filter((group) => group.names.length);
});
</script>

<style lang="scss" scoped>
.asset-delete {
  display: flex;
  width: 100%;
  flex-direction: column;
  gap: 0.75rem;
  overflow-wrap: anywhere;

  &__message {
    margin: 0;
  }

  &__group-title {
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
