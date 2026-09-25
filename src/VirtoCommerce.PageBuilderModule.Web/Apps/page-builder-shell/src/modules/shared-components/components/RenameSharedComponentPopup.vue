<template>
  <VcPopup
    :model-value="true"
    :title="$t('SHARED_COMPONENTS.RENAME.TITLE')"
    is-mobile-fullscreen
    @update:model-value="handleModelValueUpdate"
    @close="$emit('close')"
  >
    <template #content>
      <VcForm class="tw-w-full tw-text-[color:var(--neutrals-800)]">
        <VcInput
          v-model="name"
          maxlength="128"
          :label="$t('SHARED_COMPONENTS.RENAME.NAME_LABEL')"
          :placeholder="$t('SHARED_COMPONENTS.RENAME.NAME_PLACEHOLDER')"
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
          {{ $t("SHARED_COMPONENTS.RENAME.CANCEL") }}
        </VcButton>
        <VcButton
          :disabled="submitting || !!nameError || !name.trim() || name.trim() === component.name"
          @click="submit"
        >
          {{ $t("SHARED_COMPONENTS.RENAME.SAVE") }}
        </VcButton>
      </div>
    </template>
  </VcPopup>
</template>

<script lang="ts" setup>
import { computed, ref } from "vue";
import { VcButton, VcForm, VcInput, VcPopup } from "@vc-shell/framework/ui";
import type { SharedComponent } from "../types";

interface Props {
  component: SharedComponent;
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
