<template>
  <div class="pd-theme">
    <p
      v-if="theme.loadError.value"
      class="pd-theme__empty"
    >
      {{ $t("PAGE_BUILDER.DESIGNER.THEME.LOAD_FAILED") }}
    </p>
    <template v-else>
      <details
        class="pd-theme__group"
        open
      >
        <summary>
          <VcIcon
            icon="lucide-image"
            size="s"
            class="pd-theme__icon"
          />
          <span>{{ $t("PAGE_BUILDER.DESIGNER.THEME.BRAND") }}</span>
          <VcIcon
            icon="lucide-chevron-down"
            size="s"
            class="pd-theme__chevron"
          />
        </summary>
        <div class="pd-theme__fields">
          <p
            v-if="look.loadError.value"
            class="pd-theme__empty"
          >
            {{ $t("PAGE_BUILDER.DESIGNER.THEME.LOOK_LOAD_FAILED") }}
          </p>
          <template v-else>
            <div
              v-for="field in BRAND_FIELDS"
              :key="field"
              class="pd-theme__brand"
            >
              <ImageField
                :model-value="look.look.value.brand[field]"
                :label="$t(BRAND_LABELS[field].label)"
                :hint="$t(BRAND_LABELS[field].hint)"
                image
                @update:model-value="look.setBrand(field, $event)"
              />
              <VcHint
                v-if="isOverridden(overridden, brandField(field))"
                class="pd-theme__overridden"
              >
                {{ $t("PAGE_BUILDER.DESIGNER.WHITE_LABELING") }}
              </VcHint>
            </div>
          </template>
        </div>
      </details>

      <details class="pd-theme__group">
        <summary>
          <VcIcon
            icon="lucide-swatch-book"
            size="s"
            class="pd-theme__icon"
          />
          <span>{{ $t("PAGE_BUILDER.DESIGNER.THEME.COLORS") }}</span>
          <VcIcon
            icon="lucide-chevron-down"
            size="s"
            class="pd-theme__chevron"
          />
        </summary>
        <div
          v-if="!look.loadError.value"
          class="pd-theme__fields"
        >
          <div
            role="tablist"
            class="pd-theme__schemes"
          >
            <button
              v-for="s in SCHEMES"
              :key="s.id"
              type="button"
              role="tab"
              class="pd-theme__scheme"
              :class="{ 'pd-theme__scheme--on': scheme === s.id }"
              :aria-selected="scheme === s.id"
              @click="scheme = s.id"
            >
              <VcIcon
                :icon="s.icon"
                size="s"
              />
              {{ $t(s.label) }}
            </button>
          </div>
          <VcHint
            v-if="isOverridden(overridden, 'palette')"
            class="pd-theme__overridden"
          >
            {{ $t("PAGE_BUILDER.DESIGNER.WHITE_LABELING") }}
          </VcHint>
          <VcHint v-if="!tokens.length">{{ $t("PAGE_BUILDER.DESIGNER.THEME.TOKENS_PENDING") }}</VcHint>
          <VcHint v-else-if="scheme === 'dark' && !hasColors('dark')">{{ $t("PAGE_BUILDER.DESIGNER.THEME.NO_DARK") }}</VcHint>
          <VcHint v-else-if="scheme === 'dark' && !hasColors('light')">{{ $t("PAGE_BUILDER.DESIGNER.THEME.DARK_NEEDS_LIGHT") }}</VcHint>
          <VcHint v-else>{{ $t("PAGE_BUILDER.DESIGNER.THEME.PALETTE_HINT") }}</VcHint>
          <details
            v-for="family in families"
            :key="`${scheme}:${family.family}`"
            class="pd-theme__family"
            @toggle="(e: Event) => toggleFamily(family.family, (e.target as HTMLDetailsElement).open)"
          >
            <summary>
              <span
                v-for="row in family.rows.slice(0, 6)"
                :key="row.token"
                class="pd-theme__chip"
                :style="row.value ? { background: row.value } : undefined"
              />
              <span class="pd-theme__family-name">{{ family.family ? tokenLabel(family.family) : $t("PAGE_BUILDER.DESIGNER.THEME.OTHER_COLORS") }}</span>
            </summary>
            <div
              v-if="openFamilies.has(family.family)"
              class="pd-theme__tokens"
            >
              <VcColorInput
                v-for="row in family.rows"
                :key="row.token"
                :model-value="row.value ?? null"
                :label="row.known ? tokenLabel(row.token) : `${row.token} · ${$t('PAGE_BUILDER.DESIGNER.THEME.UNKNOWN_TOKEN')}`"
                size="small"
                clearable
                @update:model-value="look.setColor(scheme, row.token, $event)"
              />
            </div>
          </details>
        </div>
      </details>

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
    </template>
  </div>
</template>

<script setup lang="ts">
// Shopify's "Theme settings": the brand (logos, favicon) and the light and dark palettes, then the theme's groups of
// settings, applied live in the preview. Fields white-labeling sets for the visitor shown say so.
import { computed, ref, watch } from "vue";
import { VcButton, VcColorInput, VcHint, VcIcon, VcSelect } from "@vc-shell/framework/ui";
import type { ThemeSettings, ThemeSettingsGroup } from "../themeSettings";
import type { ThemeLookState } from "../useThemeLook";
import type { ColorScheme, PaletteTokens } from "../previewMessages";
import type { Schemas } from "../types";
import { iconOf } from "../schema";
import { BRAND_FIELDS, paletteFamilies, tokenLabel, type BrandField } from "../themeLook";
import { brandField, isOverridden } from "../whiteLabeling";
import Fields from "./Fields.vue";
import ImageField from "./ImageField.vue";

const props = defineProps<{
  theme: ThemeSettings;
  look: ThemeLookState;
  schemas: Schemas;
  storeId?: string;
  /** the tokens the storefront previewed announced; none before its handshake */
  paletteTokens?: PaletteTokens;
  /** the theme fields white-labeling sets for the visitor the preview shows */
  overridden?: string[];
}>();

const BRAND_LABELS: Record<BrandField, { label: string; hint: string }> = {
  logoUrl: { label: "PAGE_BUILDER.DESIGNER.THEME.LOGO", hint: "PAGE_BUILDER.DESIGNER.THEME.LOGO_HINT" },
  secondaryLogoUrl: { label: "PAGE_BUILDER.DESIGNER.THEME.SECONDARY_LOGO", hint: "PAGE_BUILDER.DESIGNER.THEME.SECONDARY_LOGO_HINT" },
  faviconUrl: { label: "PAGE_BUILDER.DESIGNER.THEME.FAVICON", hint: "PAGE_BUILDER.DESIGNER.THEME.FAVICON_HINT" },
};

const SCHEMES = [
  { id: "light", icon: "lucide-sun", label: "PAGE_BUILDER.DESIGNER.LIGHT" },
  { id: "dark", icon: "lucide-moon", label: "PAGE_BUILDER.DESIGNER.DARK" },
] as const;
const scheme = ref<ColorScheme>("light");
const tokens = computed(() => props.paletteTokens?.[scheme.value] ?? []);
const families = computed(() => (tokens.value.length ? paletteFamilies(tokens.value, props.look.look.value[scheme.value]) : []));
const hasColors = (part: ColorScheme) => Object.keys(props.look.look.value[part]).length > 0;
// a family's colors render only while it is open: a palette has a few hundred
const openFamilies = ref(new Set<string>());
function toggleFamily(family: string, open: boolean) {
  const next = new Set(openFamilies.value);
  if (open) next.add(family);
  else next.delete(family);
  openFamilies.value = next;
}
watch(scheme, () => (openFamilies.value = new Set()));

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
    display: flex;
    flex-direction: column;
    gap: 12px;
    padding: 4px 8px 16px;
  }

  &__brand {
    display: flex;
    flex-direction: column;
    gap: 4px;
  }

  &__overridden {
    color: var(--warning-600);
  }

  // light and dark, as the left rail's mode pills
  &__schemes {
    display: flex;
    gap: 4px;
  }

  &__scheme {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    padding: 4px 10px;
    border: 1px solid var(--neutrals-200);
    border-radius: 999px;
    background: transparent;
    color: var(--neutrals-600);
    font-size: 13px;
    cursor: pointer;

    &--on {
      border-color: var(--primary-500);
      color: var(--primary-700);
    }
  }

  &__family {
    border: 1px solid var(--neutrals-200);
    border-radius: 8px;

    summary {
      display: flex;
      align-items: center;
      gap: 4px;
      padding: 6px 8px;
      list-style: none;
      cursor: pointer;

      &::-webkit-details-marker {
        display: none;
      }
    }
  }

  // a family's first colors; an empty one is left to the storefront's preset
  &__chip {
    width: 14px;
    height: 14px;
    border: 1px solid var(--neutrals-300);
    border-radius: 4px;
  }

  &__family-name {
    flex: 1;
    margin-left: 4px;
    color: var(--neutrals-900);
  }

  &__tokens {
    display: flex;
    flex-direction: column;
    gap: 8px;
    padding: 4px 8px 12px;
  }
}
</style>
