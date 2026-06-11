<template>
  <VcPopup
    class="assets-library-create-folder-popup"
    :model-value="true"
    :title="$t('PAGE_BUILDER.ASSETS.CREATE_FOLDER.TITLE')"
    is-mobile-fullscreen
    @update:model-value="handleModelValueUpdate"
    @close="emit('close')"
  >
    <template #content>
      <VcForm class="assets-library-create-folder">
        <VcInput
          v-model="folderName"
          :label="$t('PAGE_BUILDER.ASSETS.CREATE_FOLDER.NAME_LABEL')"
          :placeholder="$t('PAGE_BUILDER.ASSETS.CREATE_FOLDER.NAME_PLACEHOLDER')"
          :error="!!folderNameError"
          :error-message="folderNameError"
          @update:model-value="emit('clear-error')"
          @keyup.enter="submit"
        >
          <template #error>
            <VcHint class="assets-library-create-folder__message assets-library-create-folder__message--error">
              {{ folderNameError }}
            </VcHint>
          </template>
          <template #hint>
            <VcHint
              aria-hidden="true"
              class="assets-library-create-folder__message"
            >
              &nbsp;
            </VcHint>
          </template>
        </VcInput>
      </VcForm>
    </template>

    <template #footer>
      <div class="tw-flex tw-justify-end tw-gap-3">
        <VcButton
          variant="secondary"
          :disabled="submitting"
          @click="emit('close')"
        >
          {{ $t("PAGE_BUILDER.ASSETS.CREATE_FOLDER.CANCEL") }}
        </VcButton>
        <VcButton
          :disabled="submitting || !!folderNameError || !folderName.trim()"
          @click="submit"
        >
          {{ $t("PAGE_BUILDER.ASSETS.CREATE_FOLDER.CREATE") }}
        </VcButton>
      </div>
    </template>
  </VcPopup>
</template>

<script lang="ts" setup>
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";
import { VcButton, VcForm, VcHint, VcInput, VcPopup } from "@vc-shell/framework/ui";

interface Props {
  submitting?: boolean;
  serverError?: string;
}

interface Emits {
  (event: "clear-error"): void;
  (event: "close"): void;
  (event: "create", name: string): void;
}

const props = withDefaults(defineProps<Props>(), {
  submitting: false,
  serverError: undefined,
});

const emit = defineEmits<Emits>();
const { t } = useI18n({ useScope: "global" });
const folderName = ref("");

const folderNameError = computed(() => {
  const value = folderName.value.trim();

  if (props.serverError) {
    return props.serverError;
  }

  if (!value) {
    return undefined;
  }

  if (value.length < 3) {
    return t("PAGE_BUILDER.ASSETS.CREATE_FOLDER.VALIDATION.MIN_LENGTH", { count: value.length });
  }

  if (value.length > 63) {
    return t("PAGE_BUILDER.ASSETS.CREATE_FOLDER.VALIDATION.MAX_LENGTH", { count: value.length });
  }

  if (value.startsWith("-")) {
    return t("PAGE_BUILDER.ASSETS.CREATE_FOLDER.VALIDATION.DASH_START");
  }

  if (value.endsWith("-")) {
    return t("PAGE_BUILDER.ASSETS.CREATE_FOLDER.VALIDATION.DASH_END");
  }

  if (value.includes("--")) {
    return t("PAGE_BUILDER.ASSETS.CREATE_FOLDER.VALIDATION.DASH_CONSECUTIVE");
  }

  if (/[^0-9a-z -]/.test(value)) {
    return t("PAGE_BUILDER.ASSETS.CREATE_FOLDER.VALIDATION.INVALID_CHARACTERS");
  }

  return undefined;
});

function submit() {
  const value = folderName.value.trim();

  if (!value || folderNameError.value) {
    return;
  }

  emit("create", value);
}

function handleModelValueUpdate(value: boolean) {
  if (!value) {
    emit("close");
  }
}
</script>

<style lang="scss" scoped>
.assets-library-create-folder-popup {
  --popup-bg: var(--additional-50);
  --popup-header-color: var(--neutrals-900);
  --popup-content-text-color: var(--neutrals-800);
  --popup-close-btn-bg: var(--neutrals-100);
  --popup-close-btn-bg-hover: var(--neutrals-200);
}

.assets-library-create-folder {
  width: min(100%, 24rem);
  color: var(--neutrals-800);

  &__message {
    display: block;
    min-height: 3rem;
    margin-top: 0.25rem;
  }

  &__message--error {
    --hint-color: var(--danger-500);
  }
}

.assets-library-create-folder-popup :deep(.vc-popup__content-inner) {
  overflow-y: visible;
  color: var(--neutrals-800);
}
</style>
