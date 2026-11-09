<template>
  <div class="pd-theme">
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
            v-for="s in COLOR_SCHEMES"
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
        <div
          v-if="tokens.length"
          class="pd-theme__generate"
        >
          <span class="pd-theme__subtitle">{{ $t("PAGE_BUILDER.DESIGNER.THEME.GENERATE_TITLE") }}</span>
          <VcHint>{{ $t("PAGE_BUILDER.DESIGNER.THEME.GENERATE_HINT") }}</VcHint>
          <VcColorInput
            v-model="brandColors.primary"
            size="small"
            :label="$t('PAGE_BUILDER.DESIGNER.THEME.GENERATE_PRIMARY')"
          />
          <VcColorInput
            v-model="brandColors.secondary"
            size="small"
            clearable
            :label="$t('PAGE_BUILDER.DESIGNER.THEME.GENERATE_SECONDARY')"
          />
          <div class="pd-theme__generate-actions">
            <VcButton
              size="sm"
              :disabled="!brandColors.primary || generating"
              :loading="generating"
              @click="generate"
            >
              {{ $t("PAGE_BUILDER.DESIGNER.THEME.GENERATE") }}
            </VcButton>
            <VcButton
              size="sm"
              variant="secondary"
              icon="lucide-undo-2"
              :disabled="!look.canUndoColors.value"
              @click="look.undoColors()"
            >
              {{ $t("PAGE_BUILDER.DESIGNER.THEME.UNDO_COLORS") }}
            </VcButton>
          </div>
          <VcHint
            v-if="generateFailed"
            class="pd-theme__error"
          >
            {{ $t("PAGE_BUILDER.DESIGNER.THEME.GENERATE_FAILED") }}
          </VcHint>
        </div>
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
        <div
          v-if="tokens.length && (contrast || contrastFailed)"
          class="pd-theme__contrast"
        >
          <span class="pd-theme__subtitle">{{ $t("PAGE_BUILDER.DESIGNER.THEME.CONTRAST_TITLE") }}</span>
          <VcHint
            v-if="contrastFailed"
            class="pd-theme__error"
          >
            {{ $t("PAGE_BUILDER.DESIGNER.THEME.CONTRAST_FAILED") }}
          </VcHint>
          <template v-else-if="contrast">
            <ul
              v-if="failingRows.length"
              class="pd-theme__pairs"
            >
              <li
                v-for="row in failingRows"
                :key="row.key"
                class="pd-theme__pair"
              >
                <span
                  class="pd-theme__sample"
                  :style="{ color: row.textColor, background: row.backgroundColor }"
                  aria-hidden="true"
                  >Aa</span
                >
                <span class="pd-theme__pair-text">
                  <span>{{ $t("PAGE_BUILDER.DESIGNER.THEME.CONTRAST_ON", { text: row.text, background: row.background }) }}</span>
                  <span class="pd-theme__ratio">
                    {{ $t("PAGE_BUILDER.DESIGNER.THEME.CONTRAST_RATIO", { ratio: row.ratio, required: row.required }) }}
                  </span>
                  <span class="pd-theme__pair-names">{{ row.pairs.join(", ") }}</span>
                </span>
              </li>
            </ul>
            <VcHint v-else-if="hasColors(scheme)">{{ $t("PAGE_BUILDER.DESIGNER.THEME.CONTRAST_OK") }}</VcHint>
            <VcHint v-if="otherFailing">
              {{ $t(scheme === "light" ? "PAGE_BUILDER.DESIGNER.THEME.CONTRAST_OTHER_DARK" : "PAGE_BUILDER.DESIGNER.THEME.CONTRAST_OTHER_LIGHT", { count: otherFailing }) }}
            </VcHint>
            <VcHint v-if="contrast.unchecked">{{ $t("PAGE_BUILDER.DESIGNER.THEME.CONTRAST_UNCHECKED", { count: contrast.unchecked }) }}</VcHint>
          </template>
        </div>
      </div>
    </details>
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from "vue";
import { debounce } from "lodash-es";
import { VcButton, VcColorInput, VcHint, VcIcon } from "@vc-shell/framework/ui";
import * as api from "../api";
import { PALETTE_CHECK_DEBOUNCE_MS, failureCount, failureRows, type PaletteCheck } from "../paletteContrast";
import type { ThemeLookState } from "../useThemeLook";
import { COLOR_SCHEMES, type ColorScheme, type PaletteTokens } from "../previewMessages";
import { BRAND_FIELDS, paletteFamilies, tokenLabel, type BrandField } from "../themeLook";
import { brandField, isOverridden } from "../whiteLabeling";
import ImageField from "./ImageField.vue";

const props = defineProps<{
  look: ThemeLookState;
  storeId?: string;
  paletteTokens?: PaletteTokens;
  overridden?: string[];
}>();

const BRAND_LABELS: Record<BrandField, { label: string; hint: string }> = {
  logoUrl: { label: "PAGE_BUILDER.DESIGNER.THEME.LOGO", hint: "PAGE_BUILDER.DESIGNER.THEME.LOGO_HINT" },
  secondaryLogoUrl: { label: "PAGE_BUILDER.DESIGNER.THEME.SECONDARY_LOGO", hint: "PAGE_BUILDER.DESIGNER.THEME.SECONDARY_LOGO_HINT" },
  faviconUrl: { label: "PAGE_BUILDER.DESIGNER.THEME.FAVICON", hint: "PAGE_BUILDER.DESIGNER.THEME.FAVICON_HINT" },
};

const scheme = ref<ColorScheme>("light");
const tokens = computed(() => props.paletteTokens?.[scheme.value] ?? []);
const families = computed(() => (tokens.value.length ? paletteFamilies(tokens.value, props.look.look.value[scheme.value]) : []));
const hasColors = (part: ColorScheme) => Object.keys(props.look.look.value[part]).length > 0;
// A family's colors render only while it is open, because a palette has a few hundred.
const openFamilies = ref(new Set<string>());
function toggleFamily(family: string, open: boolean) {
  const next = new Set(openFamilies.value);
  if (open) next.add(family);
  else next.delete(family);
  openFamilies.value = next;
}
watch(scheme, () => (openFamilies.value = new Set()));

// A later check wins over one still on its way.
const contrast = ref<PaletteCheck>();
const contrastFailed = ref(false);
let checkRun = 0;
const checkKey = () => JSON.stringify([props.storeId, props.paletteTokens, props.look.look.value.light, props.look.look.value.dark]);
let checkedKey: string | undefined;
async function checkContrast() {
  const { storeId, paletteTokens } = props;
  if (!storeId || !paletteTokens || !props.look.loaded.value) return;
  const key = checkKey();
  if (key === checkedKey) return;
  const run = ++checkRun;
  try {
    const result = await api.checkPalette(storeId, paletteTokens, props.look.look.value);
    if (run !== checkRun) return;
    contrast.value = result;
    contrastFailed.value = false;
    checkedKey = key;
  } catch {
    if (run !== checkRun) return;
    contrast.value = undefined;
    contrastFailed.value = true;
    checkedKey = undefined;
  }
}
const scheduleCheck = debounce(checkContrast, PALETTE_CHECK_DEBOUNCE_MS);
watch(() => [props.look.look.value.light, props.look.look.value.dark, props.paletteTokens, props.look.loaded.value, props.storeId], () => scheduleCheck(), {
  immediate: true,
});
onBeforeUnmount(() => scheduleCheck.cancel());
const failingRows = computed(() => failureRows(contrast.value?.failures ?? [], scheme.value));
const otherFailing = computed(() => failureCount(contrast.value?.failures ?? [], scheme.value === "light" ? "dark" : "light"));

const brandColors = ref<{ primary: string | null; secondary: string | null }>({ primary: null, secondary: null });
watch(
  () => props.look.loaded.value,
  (loaded) => {
    if (loaded && !brandColors.value.primary) brandColors.value.primary = props.look.look.value.light.color_primary_500 ?? null;
  },
  { immediate: true },
);
const generating = ref(false);
const generateFailed = ref(false);
async function generate() {
  const { primary, secondary } = brandColors.value;
  if (!primary || !props.storeId || !props.paletteTokens) return;
  generating.value = true;
  generateFailed.value = false;
  try {
    const generated = await api.generatePalette(props.storeId, props.paletteTokens, primary, secondary);
    props.look.applyGenerated(generated);
    // The generator's own failures show at once; the check that follows adds the pairs it could not check.
    checkRun++;
    contrast.value = generated.check;
    contrastFailed.value = false;
  } catch {
    generateFailed.value = true;
  } finally {
    generating.value = false;
  }
}
</script>

<style lang="scss" scoped>
.pd-theme {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 8px;

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

  &__generate,
  &__contrast {
    display: flex;
    flex-direction: column;
    gap: 8px;
    padding: 8px;
    border: 1px solid var(--neutrals-200);
    border-radius: 8px;
  }

  &__generate-actions {
    display: flex;
    gap: 8px;
  }

  &__subtitle {
    font-weight: 600;
    color: var(--neutrals-900);
  }

  &__error {
    color: var(--danger-600);
  }

  &__pairs {
    display: flex;
    flex-direction: column;
    gap: 8px;
    margin: 0;
    padding: 0;
    list-style: none;
  }

  &__pair {
    display: flex;
    align-items: flex-start;
    gap: 8px;
  }

  &__sample {
    flex: none;
    width: 32px;
    padding: 2px 0;
    border: 1px solid var(--neutrals-300);
    border-radius: 4px;
    font-weight: 600;
    text-align: center;
  }

  &__pair-text {
    display: flex;
    flex-direction: column;
    min-width: 0;
    color: var(--neutrals-900);
  }

  &__ratio {
    color: var(--danger-600);
    font-size: 12px;
  }

  &__pair-names {
    color: var(--neutrals-500);
    font-size: 12px;
  }

  &__tokens {
    display: flex;
    flex-direction: column;
    gap: 8px;
    padding: 4px 8px 12px;
  }
}
</style>
