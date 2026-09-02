<template>
  <VcPopup
    class="asset-overwrite-popup"
    :model-value="true"
    :title="$t('ASSET_LIBRARY.OVERWRITE.TITLE')"
    is-mobile-fullscreen
    @update:model-value="handleModelValueUpdate"
    @close="$emit('cancel')"
  >
    <template #content>
      <div class="asset-overwrite">
        <p class="asset-overwrite__message">
          {{ consequenceMessage }}
        </p>

        <ul
          v-if="pageNames.length"
          class="asset-overwrite__pages"
          :aria-label="$t('ASSET_LIBRARY.OVERWRITE.AFFECTED_PAGES')"
        >
          <li
            v-for="pageName in pageNames"
            :key="pageName"
          >
            {{ pageName }}
          </li>
        </ul>

        <VcForm class="asset-overwrite__form">
          <VcInput
            v-model="fileName"
            :label="$t('ASSET_LIBRARY.OVERWRITE.UPLOAD_AS_LABEL')"
            :error="!!fileNameError"
            :error-message="fileNameError"
            @update:model-value="clearServerError"
            @keyup.enter="uploadAs"
          />
        </VcForm>
      </div>
    </template>

    <template #footer>
      <div class="tw-flex tw-w-full tw-flex-wrap tw-justify-end tw-gap-3">
        <VcButton
          variant="secondary"
          :disabled="submitting"
          @click="$emit('cancel')"
        >
          {{ $t("ASSET_LIBRARY.OVERWRITE.CANCEL") }}
        </VcButton>
        <VcButton
          variant="secondary"
          :disabled="submitting"
          @click="$emit('replace')"
        >
          {{ $t("ASSET_LIBRARY.OVERWRITE.REPLACE") }}
        </VcButton>
        <VcButton
          :disabled="submitting || !!localFileNameError"
          @click="uploadAs"
        >
          {{ $t("ASSET_LIBRARY.OVERWRITE.UPLOAD_AS") }}
        </VcButton>
      </div>
    </template>
  </VcPopup>
</template>

<script lang="ts" setup>
import { computed, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { VcButton, VcForm, VcInput, VcPopup } from "@vc-shell/framework/ui";
import type { AssetUploadConflict } from "../composables/useAssetLibraryActions";

interface Props {
  conflict: AssetUploadConflict;
  submitting?: boolean;
  serverError?: string;
}

interface Emits {
  (event: "cancel"): void;
  (event: "replace"): void;
  (event: "upload-as", fileName: string): void;
  (event: "clear-error"): void;
}

const props = withDefaults(defineProps<Props>(), {
  submitting: false,
  serverError: undefined,
});
const emit = defineEmits<Emits>();
const { t } = useI18n({ useScope: "global" });
const fileName = ref(props.conflict.file.name);

watch(
  () => props.conflict.file.name,
  (value) => {
    fileName.value = value;
    emit("clear-error");
  },
);

const pageNames = computed(() => [
  ...new Set(
    props.conflict.references.referencePages
      .map((page) => page.name || page.permalink || page.id)
      .filter((name): name is string => !!name),
  ),
]);
const consequenceMessage = computed(() => {
  const count = props.conflict.references.referencesCount;

  if (count === 0) {
    return t("ASSET_LIBRARY.OVERWRITE.MESSAGE_UNUSED", { name: props.conflict.existingEntry.name });
  }

  const key = count === 1 ? "ASSET_LIBRARY.OVERWRITE.MESSAGE_USED_ONE" : "ASSET_LIBRARY.OVERWRITE.MESSAGE_USED_MANY";
  return t(key, { name: props.conflict.existingEntry.name, count });
});
const localFileNameError = computed(() => {
  const value = fileName.value.trim();

  if (!value) {
    return t("ASSET_LIBRARY.OVERWRITE.VALIDATION.REQUIRED");
  }

  if (/[\\/]/.test(value)) {
    return t("ASSET_LIBRARY.OVERWRITE.VALIDATION.INVALID");
  }

  return undefined;
});
const fileNameError = computed(() => localFileNameError.value || props.serverError);

function uploadAs() {
  const value = fileName.value.trim();

  if (localFileNameError.value) {
    return;
  }

  emit("upload-as", value);
}

function clearServerError() {
  emit("clear-error");
}

function handleModelValueUpdate(value: boolean) {
  if (!value) {
    emit("cancel");
  }
}
</script>

<style lang="scss" scoped>
.asset-overwrite-popup {
  --popup-bg: var(--additional-50);
  --popup-header-color: var(--neutrals-900);
  --popup-content-text-color: var(--neutrals-800);
  --popup-close-btn-bg: var(--neutrals-100);
  --popup-close-btn-bg-hover: var(--neutrals-200);
}

.asset-overwrite {
  display: flex;
  width: 100%;
  flex-direction: column;
  gap: 1rem;
  color: var(--neutrals-800);

  &__message {
    margin: 0;
    font-weight: 600;
  }

  &__pages {
    max-height: 12rem;
    margin: 0;
    padding-left: 1.25rem;
    overflow-y: auto;
  }

  &__form {
    width: 100%;
  }
}
</style>
