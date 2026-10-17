<template>
  <div class="pd-theme">
    <div
      v-if="theme.presets.value.length"
      class="pd-theme__presets"
    >
      <VcSelect
        v-model="preset"
        size="small"
        :label="$t('PAGE_BUILDER.DESIGNER.THEME.PRESET')"
        :placeholder="$t('PAGE_BUILDER.DESIGNER.THEME.PRESET_PLACEHOLDER')"
        :options="theme.presets.value"
        :option-value="(x: string) => x"
        :option-label="(x: string) => x"
      />
      <VcButton
        size="sm"
        variant="secondary"
        :disabled="!preset"
        @click="preset && theme.applyPreset(preset)"
      >
        {{ $t("PAGE_BUILDER.DESIGNER.THEME.APPLY") }}
      </VcButton>
    </div>

    <p
      v-if="!groups.length"
      class="pd-theme__empty"
    >
      {{ $t("PAGE_BUILDER.DESIGNER.THEME.EMPTY") }}
    </p>
    <details
      v-for="(group, i) in groups"
      :key="group.name"
      class="pd-theme__group"
      :open="i === 0"
    >
      <summary>
        <VcIcon
          :icon="iconOf({ icon: group.icon })"
          size="s"
          class="pd-theme__icon"
        />
        <span>{{ group.name }}</span>
        <VcIcon
          icon="lucide-chevron-down"
          size="s"
          class="pd-theme__chevron"
        />
      </summary>
      <Fields
        class="pd-theme__fields"
        :fields="group.settings"
        :values="theme.values.value"
        :context="{ model: theme.values.value, settings: theme.values.value, location: { params: { storeId } } }"
        @update="theme.set"
      />
    </details>
  </div>
</template>

<script setup lang="ts">
// Shopify's "Theme settings": the theme's groups of settings (logo, colours, …), applied live in the preview.
import { computed, ref, watch } from "vue";
import { VcButton, VcIcon, VcSelect } from "@vc-shell/framework/ui";
import type { ThemeSettings, ThemeSettingsGroup } from "../themeSettings";
import type { Schemas } from "../types";
import { iconOf } from "../schema";
import Fields from "./Fields.vue";

const props = defineProps<{ theme: ThemeSettings; schemas: Schemas; storeId?: string }>();

// the storefront's schema (its 'loaded' message) wins over the theme folder's, as in the Angular designer
const groups = computed<ThemeSettingsGroup[]>(() => {
  const fromPreview = props.schemas.settingsSchema?.settings_schema;
  return fromPreview?.length ? fromPreview : props.theme.serverGroups.value;
});
const preset = ref<string>();
watch([groups, () => props.theme.loaded.value], () => props.theme.loaded.value && props.theme.withDefaults(groups.value), { immediate: true });
</script>

<style lang="scss" scoped>
.pd-theme {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 8px;

  &__presets {
    display: flex;
    align-items: flex-end;
    gap: 8px;
    padding: 4px 8px 8px;
    border-bottom: 1px solid var(--neutrals-200);

    > :first-child {
      flex: 1;
    }
  }

  &__empty {
    margin: 0;
    padding: 8px;
    color: var(--neutrals-500);
  }

  &__group {
    border-bottom: 1px solid var(--neutrals-200);

    summary {
      display: flex;
      align-items: center;
      gap: 8px;
      min-height: 36px;
      padding: 0 8px;
      list-style: none;
      cursor: pointer;
      font-weight: 600;
      color: var(--neutrals-900);

      &::-webkit-details-marker {
        display: none;
      }

      span {
        flex: 1;
      }
    }

    &[open] .pd-theme__chevron {
      transform: rotate(180deg);
    }
  }

  &__icon,
  &__chevron {
    color: var(--neutrals-500);
  }

  &__fields {
    padding: 4px 8px 16px;
  }
}
</style>
