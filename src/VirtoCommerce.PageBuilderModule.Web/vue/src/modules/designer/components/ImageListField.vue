<template>
  <div class="pd-image-list">
    <div class="pd-image-list__head">
      <span class="pd-image-list__label">{{ label }}</span>
      <VcButton
        variant="ghost"
        size="sm"
        icon="lucide-plus"
        :disabled="!pickAsset"
        @click="add"
      >
        {{ $t("PAGE_BUILDER.DESIGNER.ADD_ITEM") }}
      </VcButton>
    </div>
    <div
      v-for="(item, index) in items"
      :key="index"
      class="pd-image-list__row"
    >
      <ImageField
        class="pd-image-list__field"
        :model-value="urlOf(item)"
        :image="image"
        :accept="accept"
        no-remove
        @update:model-value="replace(index, $event)"
      />
      <div class="pd-image-list__actions">
        <VcButton
          variant="ghost"
          size="icon-sm"
          icon="lucide-arrow-up"
          :disabled="index === 0"
          :aria-label="$t('PAGE_BUILDER.DESIGNER.MOVE_UP')"
          @click="move(index, -1)"
        />
        <VcButton
          variant="ghost"
          size="icon-sm"
          icon="lucide-arrow-down"
          :disabled="index === items.length - 1"
          :aria-label="$t('PAGE_BUILDER.DESIGNER.MOVE_DOWN')"
          @click="move(index, 1)"
        />
        <VcButton
          variant="ghost"
          size="icon-sm"
          icon="lucide-trash-2"
          :aria-label="$t('PAGE_BUILDER.DESIGNER.REMOVE')"
          @click="remove(index)"
        />
      </div>
    </div>
    <VcHint v-if="hint">{{ hint }}</VcHint>
  </div>
</template>

<script setup lang="ts">
import { computed, inject } from "vue";
import { VcButton, VcHint } from "@vc-shell/framework/ui";
import { isRecord } from "../json";
import { moveItem } from "../rules";
import { PickAssetKey } from "../types";
import ImageField from "./ImageField.vue";

const props = defineProps<{ modelValue?: unknown; label?: string; hint?: string; image?: boolean; accept?: string[] }>();
const emit = defineEmits<{ "update:modelValue": [value: unknown[]] }>();

const pickAsset = inject(PickAssetKey, undefined);

const items = computed<unknown[]>(() => {
  if (Array.isArray(props.modelValue)) return props.modelValue;
  return props.modelValue ? [props.modelValue] : [];
});

function urlOf(item: unknown) {
  if (typeof item === "string") return item;
  return isRecord(item) && typeof item.url === "string" ? item.url : "";
}

async function add() {
  const url = await pickAsset?.(props.accept);
  if (url) emit("update:modelValue", [...items.value, url]);
}

const replace = (index: number, url: string | null) =>
  emit(
    "update:modelValue",
    items.value.map((item, i) => (i === index ? (url ?? "") : item)),
  );
const move = (index: number, by: number) => emit("update:modelValue", moveItem(items.value, index, index + by));
const remove = (index: number) =>
  emit(
    "update:modelValue",
    items.value.filter((_, i) => i !== index),
  );
</script>

<style lang="scss" scoped>
.pd-image-list {
  display: flex;
  flex-direction: column;
  gap: 8px;

  &__head {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 8px;
  }

  &__label {
    font-size: 13px;
    font-weight: 500;
    color: var(--neutrals-900);
  }

  &__row {
    display: flex;
    align-items: flex-start;
    gap: 4px;
  }

  &__field {
    flex: 1;
    min-width: 0;
  }

  &__actions {
    display: flex;
    flex-direction: column;
  }
}
</style>
