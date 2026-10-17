<template>
  <aside
    class="vh"
    :aria-label="$t('PAGE_BUILDER.DESIGNER.HISTORY.TITLE')"
  >
    <header class="vh__header">
      <VcIcon
        icon="lucide-history"
        size="s"
        class="vh__muted"
      />
      <h3 class="vh__title">{{ $t("PAGE_BUILDER.DESIGNER.HISTORY.TITLE") }}</h3>
      <VcIcon
        v-if="loading"
        icon="lucide-refresh-cw"
        size="xs"
        class="vh__muted vh__spin"
      />
      <button
        type="button"
        class="vh__close"
        :title="$t('PAGE_BUILDER.DESIGNER.HISTORY.CLOSE')"
        :aria-label="$t('PAGE_BUILDER.DESIGNER.HISTORY.CLOSE')"
        @click="emit('close')"
      >
        <VcIcon
          icon="lucide-x"
          size="s"
        />
      </button>
    </header>

    <div class="vh__list">
      <p
        v-if="failed"
        class="vh__empty"
      >
        {{ $t("PAGE_BUILDER.DESIGNER.HISTORY.FAILED") }}
      </p>
      <p
        v-else-if="!loading && !versions.length"
        class="vh__empty"
      >
        {{ $t("PAGE_BUILDER.DESIGNER.HISTORY.EMPTY") }}
      </p>
      <div
        v-for="(version, index) in versions"
        :key="version.sha"
        class="vh__row"
        :class="{ 'vh__row--live': version.sha === liveSha, 'vh__row--previewing': version.sha === previewing }"
      >
        <!-- what and its state on top; who, when and what can be done with it below -->
        <div class="vh__line">
          <p
            class="vh__message"
            :title="version.message"
          >
            {{ version.message || version.shortSha || version.sha.slice(0, 7) }}
          </p>
          <span
            v-if="version.sha === previewing"
            class="vh__badge vh__badge--latest"
            >{{ $t("PAGE_BUILDER.DESIGNER.HISTORY.PREVIEWING") }}</span
          >
          <span
            v-else-if="index === 0 && !previewing"
            class="vh__badge vh__badge--latest"
            >{{ $t("PAGE_BUILDER.DESIGNER.HISTORY.LATEST") }}</span
          >
          <span
            v-if="version.sha === liveSha"
            class="vh__badge vh__badge--live"
            >{{ $t("PAGE_BUILDER.DESIGNER.HISTORY.PUBLISHED") }}</span
          >
          <span
            v-if="version.mine"
            class="vh__badge"
            >{{ $t("PAGE_BUILDER.DESIGNER.HISTORY.YOUR_DRAFT") }}</span
          >
        </div>
        <div class="vh__line">
          <p
            class="vh__meta"
            :title="[version.sha, when(version.date), who(version)].filter(Boolean).join(' · ')"
          >
            <span class="vh__sha">{{ version.shortSha ?? version.sha.slice(0, 7) }}</span>
            · {{ when(version.date) }}<template v-if="who(version)"> · {{ who(version) }}</template>
          </p>
          <button
            type="button"
            class="vh__action"
            :class="{ 'vh__action--on': version.sha === previewing }"
            @click="emit('preview', version.sha === previewing ? null : version)"
          >
            <VcIcon
              icon="lucide-eye"
              size="xs"
            />
            {{ $t(version.sha === previewing ? "PAGE_BUILDER.DESIGNER.HISTORY.EXIT" : "PAGE_BUILDER.DESIGNER.HISTORY.PREVIEW") }}
          </button>
          <button
            v-if="index > 0"
            type="button"
            class="vh__action vh__action--restore"
            :disabled="dirty || busy"
            :title="dirty ? $t('PAGE_BUILDER.DESIGNER.HISTORY.SAVE_FIRST') : undefined"
            @click="emit('restore', version)"
          >
            <VcIcon
              icon="lucide-rotate-ccw"
              size="xs"
            />
            {{ $t("PAGE_BUILDER.DESIGNER.HISTORY.RESTORE") }}
          </button>
        </div>
      </div>
    </div>

    <footer
      v-if="hasMore || canDiscard"
      class="vh__footer"
    >
      <button
        v-if="hasMore"
        type="button"
        class="vh__wide"
        :disabled="loading"
        @click="load(cursor)"
      >
        {{ $t("PAGE_BUILDER.DESIGNER.HISTORY.MORE") }}
      </button>
      <button
        v-if="canDiscard"
        type="button"
        class="vh__wide vh__wide--danger"
        :disabled="busy"
        @click="emit('discard')"
      >
        <VcIcon
          icon="lucide-trash-2"
          size="xs"
        />
        {{ $t("PAGE_BUILDER.DESIGNER.HISTORY.DISCARD") }}
      </button>
    </footer>
  </aside>
</template>

<script setup lang="ts">
// A page's versions, newest first, in a panel over the preview: what changed, when and by whom; which one is published
// and which is the latest; Preview shows one, Restore makes it the draft again, Discard goes back to what is published.
import { computed, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { VcIcon } from "@vc-shell/framework/ui";
import type { PageVersion } from "../api";

export interface VersionPage {
  versions: PageVersion[];
  hasMore?: boolean;
  endCursor?: string;
}

const props = defineProps<{
  /** the versions, or the next ones after a cursor */
  source: (after?: string) => Promise<VersionPage>;
  /** the version shown in the preview instead of the page */
  previewing?: string;
  dirty: boolean;
  busy: boolean;
  /** there are unpublished changes to throw away */
  canDiscard: boolean;
}>();
const emit = defineEmits<{ close: []; preview: [version: PageVersion | null]; restore: [version: PageVersion]; discard: [] }>();

const { locale } = useI18n({ useScope: "global" });
const versions = ref<PageVersion[]>([]);
const hasMore = ref(false);
const cursor = ref<string>();
const loading = ref(false);
const failed = ref(false);

// published: the newest version the storefront has (a file page lists every one its production branch holds)
const liveSha = computed(() => versions.value.find((x) => x.published)?.sha);

const who = (v: PageVersion) => v.vcUser || v.author?.login || v.author?.name || "";
const when = (date?: string) =>
  date ? new Date(date).toLocaleString(locale.value, { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" }) : "";

/** The first versions, or the next ones after `after` */
async function load(after?: string) {
  loading.value = true;
  failed.value = false;
  try {
    const page = await props.source(after);
    const known = new Set(after ? versions.value.map((x) => x.sha) : []);
    versions.value = [...(after ? versions.value : []), ...page.versions.filter((x) => !known.has(x.sha))];
    hasMore.value = !!page.hasMore && !!page.endCursor;
    cursor.value = page.endCursor;
  } catch {
    failed.value = true;
  } finally {
    loading.value = false;
  }
}

onMounted(() => load());
defineExpose({ reload: () => load() });
</script>

<style lang="scss" scoped>
.vh {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
  font-size: 13px;

  &__header {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 12px 12px 12px 16px;
    border-bottom: 1px solid var(--neutrals-200);
  }

  &__title {
    flex: 1;
    margin: 0;
    font-size: 14px;
    font-weight: 600;
    color: var(--neutrals-900);
  }

  &__muted {
    color: var(--neutrals-500);
  }

  &__spin {
    animation: vh-spin 1s linear infinite;
  }

  &__close {
    display: inline-flex;
    padding: 4px;
    border: 0;
    border-radius: 6px;
    background: none;
    color: var(--neutrals-500);
    cursor: pointer;

    &:hover {
      background: var(--neutrals-100);
      color: var(--neutrals-900);
    }
  }

  &__list {
    flex: 1;
    min-height: 0;
    overflow-y: auto;
  }

  &__empty {
    margin: 0;
    padding: 24px 12px;
    text-align: center;
    color: var(--neutrals-500);
  }

  &__row {
    display: flex;
    flex-direction: column;
    gap: 4px;
    padding: 10px 12px 10px 16px;
    border-bottom: 1px solid var(--neutrals-100);

    &:hover {
      background: var(--neutrals-100);
    }

    &--live {
      background: color-mix(in srgb, var(--success-500) 6%, transparent);
    }

    &--previewing {
      background: color-mix(in srgb, var(--primary-500) 8%, transparent);
      box-shadow: inset 0 0 0 2px var(--primary-400, var(--primary-500));
    }
  }

  &__line {
    display: flex;
    align-items: center;
    gap: 6px;
    min-width: 0;
  }

  &__message {
    flex: 1;
    min-width: 0;
    margin: 0;
    overflow: hidden;
    white-space: nowrap;
    text-overflow: ellipsis;
    font-weight: 500;
    color: var(--neutrals-900);
  }

  &__meta {
    flex: 1;
    min-width: 0;
    margin: 0;
    overflow: hidden;
    white-space: nowrap;
    text-overflow: ellipsis;
    font-size: 11px;
    color: var(--neutrals-500);
  }

  &__sha {
    font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  }

  &__badge {
    flex: none;
    padding: 1px 6px;
    border: 1px solid;
    border-radius: 4px;
    border-color: var(--neutrals-200);
    color: var(--neutrals-600);
    font-size: 10px;
    font-weight: 500;

    &--live {
      border-color: color-mix(in srgb, var(--success-500) 35%, transparent);
      background: color-mix(in srgb, var(--success-500) 10%, transparent);
      color: var(--success-700, var(--success-500));
    }

    &--latest {
      border-color: color-mix(in srgb, var(--primary-500) 35%, transparent);
      background: color-mix(in srgb, var(--primary-500) 10%, transparent);
      color: var(--primary-700, var(--primary-500));
    }
  }

  // Preview and Restore come up on the row under the pointer, as in Buyer24 (taking no room until then, so the date
  // shows in full); Exit stays while previewing
  &__action {
    flex: none;
    display: none;
    align-items: center;
    gap: 4px;
    padding: 3px 8px;
    border: 1px solid var(--neutrals-200);
    border-radius: 4px;
    background: var(--neutrals-50, #fff);
    color: var(--neutrals-700);
    font-size: 11px;
    cursor: pointer;

    &:hover {
      background: var(--neutrals-100);
    }

    &:disabled {
      cursor: not-allowed;
      opacity: 0.5;
    }

    &--on {
      display: inline-flex;
      border-color: color-mix(in srgb, var(--primary-500) 45%, transparent);
      color: var(--primary-700, var(--primary-500));
    }

    &--restore {
      border-color: color-mix(in srgb, var(--primary-500) 35%, transparent);
      color: var(--primary-700, var(--primary-500));
    }
  }

  &__row:hover &__action,
  &__row:focus-within &__action {
    display: inline-flex;
  }

  &__footer {
    display: flex;
    flex-direction: column;
    gap: 6px;
    padding: 8px 12px;
    border-top: 1px solid var(--neutrals-200);
    background: var(--neutrals-100);
  }

  &__wide {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    gap: 6px;
    padding: 6px 8px;
    border: 1px solid var(--neutrals-200);
    border-radius: 8px;
    background: var(--neutrals-50, #fff);
    color: var(--neutrals-700);
    font-size: 12px;
    cursor: pointer;

    &:disabled {
      cursor: not-allowed;
      opacity: 0.5;
    }

    &--danger {
      border-color: color-mix(in srgb, var(--danger-500) 35%, transparent);
      background: color-mix(in srgb, var(--danger-500) 6%, transparent);
      color: var(--danger-600, var(--danger-500));

      &:hover {
        background: color-mix(in srgb, var(--danger-500) 12%, transparent);
      }
    }
  }
}

@keyframes vh-spin {
  to {
    transform: rotate(360deg);
  }
}
</style>
