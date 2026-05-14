<template>
  <VcPopup
    :title="$t('PAGE_BUILDER.ASSETS.CREATE_FOLDER.TITLE')"
    is-mobile-fullscreen
    @close="emit('close')"
  >
    <template #content>
      <VcForm class="assets-library-create-folder">
        <VcInput
          v-model="folderName"
          :label="$t('PAGE_BUILDER.ASSETS.CREATE_FOLDER.NAME_LABEL')"
          :placeholder="$t('PAGE_BUILDER.ASSETS.CREATE_FOLDER.NAME_PLACEHOLDER')"
          :error="!!folderNameError"
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

interface Props {
  submitting?: boolean;
}

interface Emits {
  (event: "close"): void;
  (event: "create", name: string): void;
}

withDefaults(defineProps<Props>(), {
  submitting: false,
});

const emit = defineEmits<Emits>();
const { t } = useI18n({ useScope: "global" });
const folderName = ref("");

const folderNameError = computed(() => {
  const value = folderName.value.trim();

  if (!value) {
    return undefined;
  }

  if (value.length < 3) {
    return t("PAGE_BUILDER.ASSETS.CREATE_FOLDER.VALIDATION.MIN");
  }

  if (value.length > 63) {
    return t("PAGE_BUILDER.ASSETS.CREATE_FOLDER.VALIDATION.MAX");
  }

  if (value.startsWith("-") || value.endsWith("-")) {
    return t("PAGE_BUILDER.ASSETS.CREATE_FOLDER.VALIDATION.EDGE_DASH");
  }

  if (value.includes("--")) {
    return t("PAGE_BUILDER.ASSETS.CREATE_FOLDER.VALIDATION.DOUBLE_DASH");
  }

  if (!/^[0-9a-z -]+$/.test(value)) {
    return t("PAGE_BUILDER.ASSETS.CREATE_FOLDER.VALIDATION.CHARS");
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
</script>

<style lang="scss">
.assets-library-create-folder {
  width: min(100%, 24rem);

  &__message {
    display: block;
    min-height: 3rem;
    margin-top: 0.25rem;
  }

  &__message--error {
    --hint-color: var(--danger-500);
  }
}

.vc-popup__content-inner:has(.assets-library-create-folder) {
  overflow-y: visible;
}
</style>
