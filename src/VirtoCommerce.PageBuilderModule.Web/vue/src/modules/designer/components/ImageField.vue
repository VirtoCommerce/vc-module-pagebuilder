<template>
  <div class="pd-image">
    <span
      v-if="label"
      class="pd-image__label"
      >{{ label }}</span
    >
    <div class="pd-image__box">
      <img
        v-if="image && modelValue"
        :src="modelValue"
        alt=""
        class="pd-image__thumb"
      />
      <VcIcon
        v-else
        :icon="image ? 'lucide-image' : 'lucide-file'"
        size="xl"
        class="pd-image__empty"
      />
      <div class="pd-image__actions">
        <VcButton
          size="sm"
          variant="secondary"
          :disabled="!pickAsset"
          @click="choose"
        >
          {{ $t("PAGE_BUILDER.DESIGNER.SELECT_ASSET") }}
        </VcButton>
        <VcButton
          v-if="modelValue"
          size="sm"
          variant="ghost"
          @click="emit('update:modelValue', null)"
        >
          {{ $t("PAGE_BUILDER.DESIGNER.REMOVE") }}
        </VcButton>
      </div>
    </div>
    <VcInput
      :model-value="modelValue ?? ''"
      size="small"
      :placeholder="$t('PAGE_BUILDER.DESIGNER.ASSET_URL')"
      :hint="hint"
      @update:model-value="emit('update:modelValue', ($event as string) || null)"
    />
  </div>
</template>

<script setup lang="ts">
// An image or file field: a URL, picked from the store's assets library (the designer provides the picker).
import { inject } from "vue";
import { VcButton, VcIcon, VcInput } from "@vc-shell/framework/ui";
import { PickAssetKey } from "../types";

defineProps<{ modelValue?: string | null; label?: string; hint?: string; image?: boolean }>();
const emit = defineEmits<{ "update:modelValue": [value: string | null] }>();

const pickAsset = inject(PickAssetKey, undefined);
async function choose() {
  const url = await pickAsset?.();
  if (url) emit("update:modelValue", url);
}
</script>

<style lang="scss" scoped>
.pd-image {
  display: flex;
  flex-direction: column;
  gap: 6px;

  &__label {
    font-size: 13px;
    font-weight: 500;
    color: var(--neutrals-900);
  }

  &__box {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 10px;
    border: 1px dashed var(--neutrals-300);
    border-radius: 8px;
  }

  &__thumb {
    width: 64px;
    height: 64px;
    object-fit: cover;
    border-radius: 6px;
    background: var(--neutrals-100);
  }

  &__empty {
    width: 64px;
    height: 64px;
    display: flex;
    align-items: center;
    justify-content: center;
    border-radius: 6px;
    background: var(--neutrals-100);
    color: var(--neutrals-400);
  }

  &__actions {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: 4px;
  }
}
</style>
