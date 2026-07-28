<template>
  <VcPopup
    class="linked-components-rename-popup"
    :model-value="true"
    :title="$t('LINKED_COMPONENTS.RENAME.TITLE')"
    is-mobile-fullscreen
    @update:model-value="handleModelValueUpdate"
    @close="$emit('close')"
  >
    <template #content>
      <VcForm class="linked-components-rename-form">
        <VcInput
          v-model="name"
          maxlength="128"
          :label="$t('LINKED_COMPONENTS.RENAME.NAME_LABEL')"
          :placeholder="$t('LINKED_COMPONENTS.RENAME.NAME_PLACEHOLDER')"
          :disabled="submitting"
          :error="!!nameError"
          :error-message="nameError"
          @update:model-value="$emit('clear-error')"
          @keyup.enter="submit"
        />
      </VcForm>
    </template>

    <template #footer>
      <div class="tw-flex tw-justify-end tw-gap-3">
        <VcButton
          variant="secondary"
          :disabled="submitting"
          @click="$emit('close')"
        >
          {{ $t("LINKED_COMPONENTS.RENAME.CANCEL") }}
        </VcButton>
        <VcButton
          :disabled="submitting || !!nameError || !name.trim() || name.trim() === component.name"
          @click="submit"
        >
          {{ $t("LINKED_COMPONENTS.RENAME.SAVE") }}
        </VcButton>
      </div>
    </template>
  </VcPopup>
</template>

<script lang="ts" setup>
import { computed, ref } from "vue";
import { VcButton, VcForm, VcInput, VcPopup } from "@vc-shell/framework/ui";
import type { LinkedComponent } from "../types";

interface Props {
  component: LinkedComponent;
  submitting?: boolean;
  serverError?: string;
}

interface Emits {
  (event: "clear-error"): void;
  (event: "close"): void;
  (event: "rename", name: string): void;
}

const props = withDefaults(defineProps<Props>(), {
  submitting: false,
  serverError: undefined,
});
const emit = defineEmits<Emits>();
const name = ref(props.component.name);

const nameError = computed(() => props.serverError || undefined);

function submit() {
  const value = name.value.trim();

  if (props.submitting || !value || nameError.value || value === props.component.name) {
    return;
  }

  emit("rename", value);
}

function handleModelValueUpdate(value: boolean) {
  if (!value) {
    emit("close");
  }
}
</script>

<style lang="scss" scoped>
.linked-components-rename-popup {
  --popup-bg: var(--additional-50);
  --popup-header-color: var(--neutrals-900);
  --popup-content-text-color: var(--neutrals-800);
  --popup-close-btn-bg: var(--neutrals-100);
  --popup-close-btn-bg-hover: var(--neutrals-200);
}

.linked-components-rename-form {
  width: 100%;
  color: var(--neutrals-800);
}
</style>
