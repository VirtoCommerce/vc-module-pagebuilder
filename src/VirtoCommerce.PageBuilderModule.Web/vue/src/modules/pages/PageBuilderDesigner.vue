<template>
  <VcBlade
    :width="sitePanel ? '60%' : '100%'"
    :class="{ 'pd-full-view': fullView }"
    :title="title"
    :subtitle="subtitle"
    :toolbar-items="toolbar"
    :modified="unsaved"
    :loading="(!!param && !group) || designer.busy.value === 'loading'"
  >
    <div
      v-if="designer.loadError.value || archived"
      class="pd-message"
    >
      <VcIcon
        icon="lucide-triangle-alert"
        size="xxl"
        variant="danger"
      />
      <p>{{ $t(archived ? "PAGE_BUILDER.DESIGNER.ARCHIVED" : "PAGE_BUILDER.DESIGNER.LOAD_FAILED") }}</p>
    </div>

    <div
      v-else
      ref="designerRoot"
      class="pd"
      :class="{ 'pd--fullscreen': device === 'fullscreen', 'pd--full-view': fullView }"
    >
      <div class="pd__left">
        <p
          v-if="designer.shared.value"
          class="pd__notice"
        >
          <VcIcon
            icon="lucide-link"
            size="s"
          />
          {{ $t("PAGE_BUILDER.DESIGNER.EDITING_SHARED") }}
        </p>
        <div
          role="tablist"
          class="pd__modes"
        >
          <button
            v-for="m in shownModes"
            :key="m.id"
            type="button"
            role="tab"
            class="pd__mode"
            :class="{ 'pd__mode--on': mode === m.id }"
            :aria-selected="mode === m.id"
            @click="mode = m.id"
          >
            <VcIcon
              :icon="m.icon"
              size="s"
            />
            {{ $t(m.label) }}
          </button>
        </div>
        <SectionTree
          v-show="mode === 'sections'"
          :designer="designer"
          @hover="(id) => bridge.send({ type: 'hover', sectionId: id })"
          @preview-section="previewSection"
          @paste="pasteFromClipboard"
          @save-shared="sharedName = ''"
          @remove-checked="removeChecked"
        />
        <ThemePanel
          v-if="mode === 'theme'"
          :theme="theme"
          :schemas="designer.schemas.value"
          :store-id="designer.storeId.value"
        />
        <SharedPanel
          v-if="mode === 'shared'"
          ref="sharedPanel"
          :store-id="designer.storeId.value"
          @insert="insertShared"
          @edit="(component) => editShared(component.id)"
        />
      </div>

      <main class="pd__canvas">
        <div class="pd__canvas-bar">
          <template v-if="fullView">
            <VcButton
              variant="ghost"
              size="icon-sm"
              icon="lucide-minimize-2"
              :title="$t('PAGE_BUILDER.DESIGNER.EXIT_FULL_VIEW')"
              :aria-label="$t('PAGE_BUILDER.DESIGNER.EXIT_FULL_VIEW')"
              @click="fullView = false"
            />
            <!-- the blades this designer was opened from, each a way back (the shell's header is hidden in full view) -->
            <nav
              v-if="parentCrumbs.length"
              class="pd__crumbs"
              :aria-label="$t('PAGE_BUILDER.DESIGNER.BACK')"
            >
              <template
                v-for="crumb in parentCrumbs"
                :key="crumb.id"
              >
                <button
                  type="button"
                  class="pd__crumb"
                  @click="closeChildren(crumb.id)"
                >
                  {{ crumb.title }}
                </button>
                <VcIcon
                  icon="lucide-chevron-right"
                  size="xs"
                  class="pd__crumb-sep"
                />
              </template>
            </nav>
            <span class="pd__name">
              <strong>{{ title }}</strong>
              <small>{{ subtitle }}</small>
            </span>
          </template>
          <template v-if="previewing">
            <span
              class="pd__status pd__status--viewing"
              :title="previewing.message"
            >
              <VcIcon
                icon="lucide-history"
                size="xs"
              />
              {{
                $t("PAGE_BUILDER.DESIGNER.HISTORY.VIEWING", {
                  version: previewing.shortSha ?? previewing.sha.slice(0, 7),
                  date: previewing.date ? new Date(previewing.date).toLocaleString(undefined, { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" }) : "",
                })
              }}
            </span>
            <VcButton
              size="sm"
              variant="secondary"
              icon="lucide-rotate-ccw"
              :disabled="unsaved || !!designer.busy.value"
              :title="unsaved ? $t('PAGE_BUILDER.DESIGNER.HISTORY.SAVE_FIRST') : undefined"
              @click="restoreVersion(previewing)"
            >
              {{ $t("PAGE_BUILDER.DESIGNER.HISTORY.RESTORE") }}
            </VcButton>
            <VcButton
              size="sm"
              variant="ghost"
              icon="lucide-undo-2"
              @click="previewVersion(null)"
            >
              {{ $t("PAGE_BUILDER.DESIGNER.HISTORY.BACK") }}
            </VcButton>
          </template>
          <span
            v-else
            class="pd__status"
            :class="`pd__status--${statusKind}`"
            >{{ $t(`PAGE_BUILDER.DESIGNER.STATUS.${statusKind.toUpperCase()}`) }}</span
          >
          <span class="pd__spacer" />
          <button
            type="button"
            class="pd__inspector"
            :class="{ 'pd__inspector--on': inspector }"
            :aria-pressed="inspector"
            :title="`${$t(inspector ? 'PAGE_BUILDER.DESIGNER.INSPECTOR_OFF' : 'PAGE_BUILDER.DESIGNER.INSPECTOR_ON')} (Shift+I)`"
            :aria-label="$t(inspector ? 'PAGE_BUILDER.DESIGNER.INSPECTOR_OFF' : 'PAGE_BUILDER.DESIGNER.INSPECTOR_ON')"
            @click="toggleInspector"
          >
            <VcIcon
              icon="lucide-square-dashed-mouse-pointer"
              size="s"
            />
          </button>
          <div
            role="radiogroup"
            class="pd__devices"
            :aria-label="$t('PAGE_BUILDER.DESIGNER.DEVICE')"
          >
            <button
              v-for="d in devices"
              :key="d.id"
              type="button"
              role="radio"
              class="pd__device"
              :class="{ 'pd__device--on': device === d.id }"
              :aria-checked="device === d.id"
              :title="$t(d.label)"
              :aria-label="$t(d.label)"
              @click="device = d.id"
            >
              <VcIcon
                :icon="d.icon"
                size="s"
              />
            </button>
          </div>
          <div
            v-if="fullView"
            class="pd__actions"
          >
            <VcButton
              v-for="action in barActions"
              :key="action.id"
              :variant="action.variant"
              :size="action.label ? 'sm' : 'icon-sm'"
              :icon="action.label ? undefined : action.icon"
              :disabled="!!action.disabled"
              :title="action.title"
              :aria-label="action.title"
              @click="action.clickHandler?.()"
            >
              {{ action.label }}
            </VcButton>
          </div>
        </div>
        <div class="pd__stage">
          <div
            class="pd__frame"
            :class="`pd__frame--${device}`"
          >
            <!-- credentialless: the platform's pages require embedded documents to opt in to embedding (COEP), which a
                 storefront doesn't; an anonymous frame needs no opt-in, so any deployed storefront previews as is -->
            <iframe
              v-if="url"
              ref="frame"
              credentialless
              :src="url"
              :title="$t('PAGE_BUILDER.DESIGNER.PREVIEW')"
              @load="bridge.onFrameLoad"
            />
            <!-- until the site answers (a new or reloaded preview): what is going on, not a blank page -->
            <div
              v-if="url && !bridge.connected.value"
              class="pd__frame-wait"
              role="status"
            >
              <template v-if="!previewSlow">
                <VcIcon
                  icon="lucide-loader-circle"
                  size="xl"
                  class="pd__spin"
                />
                <p>{{ $t("PAGE_BUILDER.DESIGNER.PREVIEW_LOADING") }}</p>
              </template>
              <template v-else>
                <VcIcon
                  icon="lucide-monitor-off"
                  size="xl"
                />
                <p>{{ $t("PAGE_BUILDER.DESIGNER.PREVIEW_SLOW", { url: storefront?.url ?? "" }) }}</p>
                <VcButton
                  variant="secondary"
                  size="sm"
                  icon="lucide-refresh-cw"
                  @click="reloadPreview"
                >
                  {{ $t("PAGE_BUILDER.DESIGNER.RELOAD_PREVIEW") }}
                </VcButton>
              </template>
            </div>
            <div
              v-else-if="storefrontChecked"
              class="pd-message"
            >
              <VcIcon
                icon="lucide-monitor-off"
                size="xxl"
              />
              <p>{{ $t("PAGE_BUILDER.DESIGNER.NO_STOREFRONT") }}</p>
            </div>
          </div>
        </div>
      </main>

      <div
        v-if="historyOpen"
        class="pd__right"
      >
        <VersionHistory
          ref="versionHistory"
          :source="historySource"
          :previewing="previewing?.sha"
          :dirty="unsaved"
          :busy="!!designer.busy.value"
          :can-discard="hasDraft"
          @close="closeHistory"
          @preview="previewVersion"
          @restore="restoreVersion"
          @discard="discardChanges"
        />
      </div>
      <div
        v-else-if="designer.selection.value"
        class="pd__right"
      >
        <SettingsPanel
          :designer="designer"
          :selection="designer.selection.value"
          @close="designer.selection.value = null"
          @edit-shared="editShared"
          @copy="copySelected"
          @paste="pasteFromClipboard"
        />
      </div>
    </div>

    <VcPopup
      v-if="pasteText !== undefined"
      :model-value="true"
      :title="$t('PAGE_BUILDER.DESIGNER.CLIPBOARD.PASTE_TITLE')"
      @close="pasteText = undefined"
    >
      <template #content>
        <div class="pd-dialog">
          <p>{{ $t("PAGE_BUILDER.DESIGNER.CLIPBOARD.PASTE_HINT") }}</p>
          <VcTextarea
            v-model="pasteText"
            class="pd-dialog__code"
          />
          <VcButton
            :disabled="!pasteText"
            @click="pasteText && pasteFrom(pasteText) && (pasteText = undefined)"
          >
            {{ $t("PAGE_BUILDER.DESIGNER.CLIPBOARD.PASTE") }}
          </VcButton>
        </div>
      </template>
    </VcPopup>

    <VcPopup
      v-if="sharedName !== undefined"
      :model-value="true"
      :title="$t('PAGE_BUILDER.DESIGNER.SELECT_MODE.SAVE_SHARED')"
      @close="sharedName = undefined"
    >
      <template #content>
        <div class="pd-dialog">
          <p>{{ $t("PAGE_BUILDER.DESIGNER.SELECT_MODE.SAVE_SHARED_HINT") }}</p>
          <VcInput
            v-model="sharedName"
            :label="$t('PAGE_BUILDER.DESIGNER.SELECT_MODE.NAME')"
            :maxlength="128"
            required
          />
          <VcButton
            :disabled="!sharedName?.trim()"
            @click="saveShared"
          >
            {{ $t("PAGE_BUILDER.DESIGNER.SAVE") }}
          </VcButton>
        </div>
      </template>
    </VcPopup>


    <VcPopup
      v-if="picking"
      v-model="picking"
      class="pd-picker"
      modal-width="tw-max-w-5xl"
      :title="$t('PAGE_BUILDER.DESIGNER.PICK_ASSET')"
      is-mobile-fullscreen
      @close="picked(undefined)"
    >
      <template #content>
        <div class="pd-picker__body">
          <AssetsLibraryContent
            pickable
            @pick="picked"
          />
        </div>
      </template>
    </VcPopup>
  </VcBlade>
</template>

<script lang="ts" setup>
import { computed, inject, onBeforeUnmount, onMounted, provide, ref, watch, watchEffect } from "vue";
import { useEventListener, useLocalStorage } from "@vueuse/core";
import { useI18n } from "vue-i18n";
import { debounce } from "lodash-es";
import {
  BladeMaximizedKey,
  hotkey,
  notification,
  useApiClient,
  useBeforeUnload,
  useBlade,
  useBladeStack,
  usePopup,
  type BladeDescriptor,
  type IBladeToolbar,
} from "@vc-shell/framework";
import { VcBlade, VcButton, VcIcon, VcInput, VcPopup, VcTextarea } from "@vc-shell/framework/ui";
import { fromClipboard, toClipboard } from "../designer/clipboard";
import { PageBuilderPageClient, type GroupedPageBuilderPage } from "../../api_client/virtocommerce.pagebuildermodule";
import VersionHistory from "../designer/components/VersionHistory.vue";
import { notifyPagesChanged, useUrlParams } from "../page-builder/composables";
import AssetsLibraryContent from "../asset-library/components/AssetsLibraryContent.vue";
import { PageAnchorsKey, PickAssetKey } from "../designer/types";
import { pageAnchors } from "../designer/anchors";
import { useDesigner } from "../designer/useDesigner";
import { resolveRefs } from "../designer/document";
import { pageOnly } from "../designer/groups";
import { previewUrl, usePreviewBridge } from "../designer/preview";
import {
  loadStorefront,
  storeTheme,
  filePreviewUrl,
  isGitFlow,
  fileHistory,
  groupHistory,
  groupVersion,
  restoreGroupVersion,
  ApiError, type FilePage, type PageVersion, type SharedComponent } from "../designer/api";
import { newItem } from "../designer/schema";
import SectionTree from "../designer/components/SectionTree.vue";
import SettingsPanel from "../designer/components/SettingsPanel.vue";
import ThemePanel from "../designer/components/ThemePanel.vue";
import SharedPanel from "../designer/components/SharedPanel.vue";
import { useSharedComponentDesigner } from "../page-builder/composables/usePageDesigner";
import { useThemeSettings } from "../designer/themeSettings";

// The page designer, a native blade in the spirit of Shopify's theme editor: the page's sections on the left, the
// storefront's own preview in the middle, the selected item's settings on the right. It opens a database page
// (param = page group id) or a page kept as a file (query: storeId, path, type: a site's Files > Pages); with the
// store's pages in git, a file page also has its version history.
defineBlade({
  name: "PageBuilderDesigner",
  url: "/page-builder-designer",
});

const { t } = useI18n({ useScope: "global" });
const { param, query, onBeforeClose, openBlade } = useBlade();
const { showConfirmation } = usePopup();
const designer = useDesigner();
// Shopify's left rail: the page's sections, the theme's settings, or the site's shared components
const theme = useThemeSettings();
const modes = [
  { id: "sections", icon: "lucide-layout-list", label: "PAGE_BUILDER.DESIGNER.SECTIONS" },
  { id: "theme", icon: "lucide-palette", label: "PAGE_BUILDER.DESIGNER.THEME.TITLE" },
  { id: "shared", icon: "lucide-link", label: "PAGE_BUILDER.DESIGNER.MODE_SHARED" },
] as const;
const mode = ref<(typeof modes)[number]["id"]>("sections");
// a shared component can't hold another one
const shownModes = computed(() => (designer.shared.value ? modes.filter((m) => m.id !== "shared") : modes));
/** unsaved page or theme edits */
const unsaved = computed(() => designer.dirty.value || theme.dirty.value);

const maximized = inject(BladeMaximizedKey, null);
if (maximized) maximized.value = true;
// full view: the designer covers the whole window, without the shell's menu and header; remembered for next time.
// Over the shell's chrome (z-index 40-60), under its dropdowns (100+) and popups (9000+).
const fullView = useLocalStorage("vc-page-designer-full-view", false);
// full view's path back: the open blades before this one (Sites › B2B-store …); going back closes the designer, which
// asks first about unsaved edits
const { blades, closeChildren } = useBladeStack();
const parentCrumbs = computed(() =>
  blades.value
    .filter((b: BladeDescriptor) => b.visible)
    .slice(0, -1)
    .map((b: BladeDescriptor) => ({ id: b.id, title: b.title ?? b.name })),
);
const FULL_VIEW_STYLE: Record<string, string> = {
  position: "fixed",
  inset: "0",
  "z-index": "90",
  // not width: VcBlade re-renders its own inline width, and the shells' maximized widths are !important
  "min-width": "100%",
  margin: "0",
  "border-radius": "0",
  "box-shadow": "none",
};

const group = ref<GroupedPageBuilderPage>();
const fileName = computed(() => designer.file.value?.path.split("/").pop()?.replace(/\.page(-draft)?$/, ""));
const title = computed(
  () => designer.shared.value?.name ?? group.value?.name ?? ((designer.doc.value.settings.name as string | undefined) || fileName.value || ""),
);
const cultureName = computed(() => group.value?.cultureName ?? query.value?.cultureName);
// an archived page isn't edited (as before): restore it first
const archived = computed(() => group.value?.status === "Archived");

type StatusKind = "unsaved" | "draft" | "published" | "changes" | "pending" | "awaiting" | "shared";
const statusKind = computed<StatusKind>(() => {
  if (unsaved.value) return "unsaved";
  // a shared component isn't published on its own: it shows wherever a page places it
  if (designer.shared.value) return "shared";
  const status = designer.status.value;
  // git: a pull request is open
  if (status.awaitingMerge) return "awaiting";
  if (status.pending) return "pending";
  if (!status.published) return "draft";
  return status.hasChanges ? "changes" : "published";
});
const subtitle = computed(() => {
  const shared = designer.shared.value;
  if (shared) return `${t("PAGE_BUILDER.DESIGNER.SHARED_COMPONENT")} · ${t("PAGE_BUILDER.DESIGNER.USED_ON", { count: shared.usageCount ?? 0 })}`;
  return designer.file.value
    ? designer.file.value.path
    : [group.value?.permalink && `/${group.value.permalink.replace(/^\//, "")}`, cultureName.value].filter(Boolean).join(" · ");
});

// ── preview ──────────────────────────────────────────────────────────────────

const devices = [
  { id: "desktop", icon: "lucide-monitor", label: "PAGE_BUILDER.DESIGNER.DESKTOP" },
  { id: "tablet", icon: "lucide-tablet", label: "PAGE_BUILDER.DESIGNER.TABLET" },
  { id: "phone", icon: "lucide-smartphone", label: "PAGE_BUILDER.DESIGNER.PHONE" },
  { id: "fullscreen", icon: "lucide-maximize", label: "PAGE_BUILDER.DESIGNER.FULLSCREEN" },
] as const;
const device = ref<(typeof devices)[number]["id"]>("desktop");

const frame = ref<HTMLIFrameElement>();
const url = ref<string>();
const storefront = ref<{ url?: string; previewPath: string }>();
const storefrontChecked = ref(false);

const selectedSectionId = () => {
  const sel = designer.selection.value;
  if (!sel || sel.kind === "page") return undefined;
  return sel.kind === "section" ? sel.id : sel.sectionId;
};
// a render: the page, its language, the section names the preview outlines, and the selection it outlines
const renderMessage = () => ({
  ...designer.preview.value,
  cultureName: cultureName.value,
  sectionLabels: designer.sectionLabels.value,
  sectionId: selectedSectionId() ?? "",
});

const bridge = usePreviewBridge({
  frame,
  url,
  onLoaded: (schemas) => {
    designer.schemas.value = schemas;
    // the header and footer first: the page's outlines take theirs in
    if (designer.groupsPreview.value) bridge.send({ type: "groups", ...designer.groupsPreview.value });
    bridge.send({ type: "page", ...renderMessage() });
    // a reloaded preview keeps showing theme edits not saved yet, and the inspector off
    if (theme.dirty.value) bridge.send({ type: "settings", settings: theme.settings.value });
    if (!inspector.value) bridge.send({ type: "inspector", enabled: false });
  },
  // a click on a section (or a shared-component placement) in the preview selects it; hovering lights its row
  onSelect: (id) => {
    selectedInPreview = true;
    designer.selection.value = { kind: "section", id };
  },
  onHover: (id) => (designer.hovered.value = id),
  // a text edited on the page: the setting changes, and its section or block is selected (the preview stays put)
  onEdit: ({ itemId, path, value }) => {
    const target = designer.setText(itemId, path, value);
    if (!target || JSON.stringify(target) === JSON.stringify(designer.selection.value)) return;
    selectedInPreview = true;
    designer.selection.value = target;
  },
});

// the header and footer re-render as their sections change (debounced while typing)
const sendGroups = debounce(() => designer.groupsPreview.value && bridge.send({ type: "groups", ...designer.groupsPreview.value }), 120);
watch(() => designer.groupsPreview.value, sendGroups, { deep: true });

// The preview's handshake takes a moment (the storefront's first build longer); one that doesn't come says so, with
// the storefront's address, as a storefront that isn't running never answers.
const PREVIEW_SLOW_MS = 20000;
const previewSlow = ref(false);
let previewTimer: ReturnType<typeof setTimeout> | undefined;
function waitForPreview() {
  clearTimeout(previewTimer);
  previewSlow.value = false;
  if (!bridge.connected.value && url.value) previewTimer = setTimeout(() => (previewSlow.value = true), PREVIEW_SLOW_MS);
}
watch([() => bridge.connected.value, url], waitForPreview, { immediate: true });
onBeforeUnmount(() => clearTimeout(previewTimer));
function reloadPreview() {
  if (frame.value && url.value) frame.value.src = url.value;
  waitForPreview();
}

// every edit re-renders the preview (debounced while typing)
const sendChanged = debounce(() => bridge.send({ type: "changed", ...renderMessage() }), 120);
watch([() => designer.preview.value, () => designer.sectionLabels.value], sendChanged);
// no section selected (a page row, or nothing): the preview drops its section outline
watch(
  () => !selectedSectionId(),
  (none) => none && sendChanged(),
);

// the inspector: on, the preview outlines sections and a click selects one; off, it works like the live page.
// A page left while it was off (a link that reloads) comes back when it is turned on again.
const inspector = ref(true);
function toggleInspector() {
  inspector.value = !inspector.value;
  designer.hovered.value = null;
  if (inspector.value && !bridge.connected.value && frame.value && url.value) frame.value.src = url.value;
  bridge.send({ type: "inspector", enabled: inspector.value });
}

// Set on the blade's element, inline and !important: the shells pin a maximized blade with !important rules of their
// own (vc-shell's, the admin shell's 8px inset), and VcBlade passes no style attribute through
const designerRoot = ref<HTMLElement>();
watchEffect(() => {
  const blade = designerRoot.value?.closest<HTMLElement>(".vc-blade");
  for (const [property, value] of Object.entries(FULL_VIEW_STYLE)) {
    if (fullView.value) blade?.style.setProperty(property, value, "important");
    else blade?.style.removeProperty(property);
  }
});

// Shift+I switches the inspector, Esc deselects (the settings panel hides); not while typing or in a dialog
// first, in the capture phase: the shell closes the blade on Esc, which here only deselects
useEventListener(
  window,
  "keydown",
  (event: KeyboardEvent) => {
    const target = event.target as HTMLElement | null;
    const root = designerRoot.value;
    // the designer, or what holds it (the page, the blade): not a dialog over it
    if (!target || !root || !(root.contains(target) || target.contains(root))) return;
    if (target.isContentEditable || ["INPUT", "TEXTAREA", "SELECT"].includes(target.tagName)) return;
    if (event.shiftKey && event.code === "KeyI" && !event.ctrlKey && !event.metaKey && !event.altKey) {
      event.preventDefault();
      toggleInspector();
    } else if (event.key === "Escape" && designer.selection.value) {
      event.stopImmediatePropagation();
      designer.selection.value = null;
    }
  },
  { capture: true },
);

// selecting a section scrolls the preview to it: a 'page' render does, once the section is on the page (a new one too).
// A click in the preview itself only moves the outline: the page stays where the pointer is.
let selectedInPreview = false;
watch(
  () => selectedSectionId(),
  (id) => {
    const fromPreview = selectedInPreview;
    selectedInPreview = false;
    if (!id) return;
    sendChanged.cancel();
    bridge.send({ type: fromPreview ? "changed" : "page", ...renderMessage() });
  },
);

// theme edits apply live (colours become CSS variables in the storefront); untouched, the preview keeps the site's own look
watch(
  () => theme.settings.value,
  (settings) => theme.dirty.value && bridge.send({ type: "settings", settings }),
);

// the Add section list: the hovered type shows at the end of the page until the pointer leaves
function previewSection(type: string | null) {
  if (type) bridge.send({ type: "preview", ...renderMessage(), model: newItem(type, "section", designer.schemas.value) });
  else sendChanged();
}

// ── assets ───────────────────────────────────────────────────────────────────

// image fields pick from the store's assets library, in a popup
const picking = ref(false);
let resolvePick: ((value: string | undefined) => void) | undefined;
function picked(asset: string | undefined) {
  resolvePick?.(asset);
  resolvePick = undefined;
  picking.value = false;
}
provide(
  PageAnchorsKey,
  computed(() =>
    pageAnchors(designer.doc.value.content, (item, kind) => (kind === "block" ? designer.blockSchema(item.type) : designer.sectionSchema(item.type))),
  ),
);
provide(PickAssetKey, () => {
  resolvePick?.(undefined);
  picking.value = true;
  return new Promise((resolve) => (resolvePick = resolve));
});

// ── actions ──────────────────────────────────────────────────────────────────

async function save() {
  try {
    if (theme.dirty.value) await theme.save();
  } catch {
    notification.error(t("PAGE_BUILDER.DESIGNER.THEME.SAVE_FAILED"));
    return;
  }
  if (!designer.dirty.value) return;
  try {
    await designer.save();
    notifyPagesChanged();
  } catch (e) {
    const messages: Record<number, string> = { 412: "PAGE_BUILDER.DESIGNER.CONFLICT", 428: "PAGE_BUILDER.DESIGNER.NO_VERSION" };
    notification.error(t(messages[e instanceof ApiError ? e.status : 0] ?? "PAGE_BUILDER.DESIGNER.SAVE_FAILED"));
  }
}

async function publish(live: boolean, rebase = false) {
  try {
    await designer.publish(live, rebase);
    notifyPagesChanged();
  } catch (e) {
    // git: someone published another version meanwhile; publishing ours over it is the editor's call
    const conflict = e instanceof ApiError && e.status === 409 ? (JSON.parse(e.body || "{}") as { canRebase?: boolean }) : undefined;
    if (live && !rebase && conflict?.canRebase) {
      if (await showConfirmation(t("PAGE_BUILDER.DESIGNER.PUBLISH_CONFLICT"))) await publish(true, true);
      else notification.error(t("PAGE_BUILDER.DESIGNER.NOTHING_PUBLISHED"));
      return;
    }
    notification.error(t("PAGE_BUILDER.DESIGNER.PUBLISH_FAILED"));
  }
}

async function promote() {
  try {
    await designer.promote();
  } catch {
    notification.error(t("PAGE_BUILDER.DESIGNER.PROMOTE_FAILED"));
  }
}

function openPreview() {
  const file = designer.file.value;
  if (file) {
    const href = filePreviewUrl(file, designer.gitFlow.value, storefront.value?.url, !!designer.status.value.hasChanges);
    if (href) window.open(href, "_blank");
  } else if (storefront.value?.url && designer.groupId.value) {
    window.open(`${storefront.value.url.replace(/\/+$/, "")}/designer-preview?pageId=${encodeURIComponent(designer.groupId.value)}`, "_blank");
  }
}

// ── shared components ────────────────────────────────────────────────────────

const sharedPanel = ref<InstanceType<typeof SharedPanel>>();
const openSharedComponentDesigner = useSharedComponentDesigner();

/** places a shared component from the Shared tab; it shows selected in the page's sections */
function insertShared(component: SharedComponent) {
  void designer.insertShared(component);
  mode.value = "sections";
}

/** edits a shared component in its own designer; once that closes, this page shows its new sections */
function editShared(componentId: string) {
  const storeId = designer.storeId.value;
  if (!storeId) return;
  openSharedComponentDesigner(storeId, componentId, async () => {
    await designer.loadComponents(true);
    sendChanged();
    sharedPanel.value?.reload();
  });
}

// ── clipboard and several sections at once ──────────────────────────────────

async function copySelected() {
  const sel = designer.selection.value;
  const item = designer.selectedItem.value;
  if (!sel || sel.kind === "page" || !item) return;
  try {
    await navigator.clipboard.writeText(toClipboard(sel.kind, item));
    notification.success(t("PAGE_BUILDER.DESIGNER.CLIPBOARD.COPIED"));
  } catch {
    notification.error(t("PAGE_BUILDER.DESIGNER.CLIPBOARD.COPY_FAILED"));
  }
}

/** a paste box when the browser won't let the page read the clipboard */
const pasteText = ref<string>();
async function pasteFromClipboard() {
  let text: string;
  try {
    text = await navigator.clipboard.readText();
  } catch {
    pasteText.value = "";
    return;
  }
  if (!pasteFrom(text)) pasteText.value = text;
}
function pasteFrom(text: string): boolean {
  const copied = fromClipboard(text);
  if (!copied) {
    notification.error(t("PAGE_BUILDER.DESIGNER.CLIPBOARD.INVALID"));
    return false;
  }
  const problem = designer.paste(copied);
  if (problem) notification.error(t(problem));
  return !problem;
}

async function removeChecked() {
  const count = designer.checked.value.length;
  if (await showConfirmation(t("PAGE_BUILDER.DESIGNER.SELECT_MODE.CONFIRM_DELETE", { count }))) designer.removeChecked();
}

const sharedName = ref<string>();
async function saveShared() {
  const name = sharedName.value?.trim();
  if (!name) return;
  try {
    const problem = await designer.saveCheckedAsShared(name);
    if (problem) {
      notification.error(t(problem));
      return;
    }
    sharedName.value = undefined;
    notification.success(t("PAGE_BUILDER.DESIGNER.SELECT_MODE.SHARED_SAVED", { name }));
  } catch {
    notification.error(t("PAGE_BUILDER.DESIGNER.SELECT_MODE.SHARED_FAILED"));
  }
}

// ── version history: a file page in git, or a database page of a site that keeps its versions ──────────────

const historyOpen = ref(false);
/** the site keeps its database pages' versions (a file page's are there when it is in git) */
const historyOn = ref(false);
const historyAvailable = computed(() => designer.gitFlow.value || (!!designer.groupId.value && historyOn.value));
/** a version the preview shows instead of the page */
const previewing = ref<PageVersion>();
const versionHistory = ref<{ reload: () => Promise<void> }>();

function historySource(after?: string) {
  const file = designer.file.value;
  if (file) return fileHistory(file, after).then((x) => ({ versions: x.versions, hasMore: x.truncated, endCursor: x.endCursor }));
  return groupHistory(designer.groupId.value!, after);
}

function openHistory() {
  designer.selection.value = null;
  historyOpen.value = true;
}

function closeHistory() {
  historyOpen.value = false;
}

// a section picked in the tree: its settings take the right column, and the page shows again
watch(
  () => designer.selection.value,
  (selection) => {
    if (!selection) return;
    historyOpen.value = false;
    if (previewing.value) previewVersion(null);
  },
);

/** Shows a version in the preview (a database page), or the page again; a file page's version opens on the storefront */
async function previewVersion(version: PageVersion | null) {
  const file = designer.file.value;
  if (version && file) {
    window.open(filePreviewUrl(file, true, undefined, false, version.sha), "_blank");
    return;
  }
  sendChanged.cancel();
  if (!version) {
    previewing.value = undefined;
    bridge.send({ type: "page", ...renderMessage() });
    bridge.send({ type: "inspector", enabled: inspector.value });
    return;
  }
  try {
    const saved = await groupVersion(designer.groupId.value!, version.sha);
    previewing.value = version;
    // as the page's own render is made: without the header and footer sections (the preview shows the site's), with
    // shared components filled in from the ones the designer has
    const shared = new Map([...designer.components].map(([id, x]) => [id, x && { content: x.content, label: x.name }]));
    // the version first (it ends any text being edited in the preview), then a look-only preview: no outlines,
    // no clicks, nothing typed into a version that is not the page
    bridge.send({ type: "page", ...renderMessage(), ...resolveRefs(pageOnly(saved), shared), sectionId: "" });
    bridge.send({ type: "inspector", enabled: false });
  } catch {
    notification.error(t("PAGE_BUILDER.DESIGNER.HISTORY.PREVIEW_FAILED"));
  }
}
// an edit shows the page again, and the preview takes clicks again
watch(
  () => designer.preview.value,
  () => {
    if (!previewing.value) return;
    previewing.value = undefined;
    bridge.send({ type: "inspector", enabled: inspector.value });
  },
);

async function reopen() {
  const file = designer.file.value;
  await (file ? designer.open(file) : designer.open({ groupId: designer.groupId.value!, storeId: designer.storeId.value! }));
  previewing.value = undefined;
  await versionHistory.value?.reload();
}

async function restoreVersion(version: PageVersion) {
  const label = version.shortSha ?? version.sha.slice(0, 7);
  if (!(await showConfirmation(t("PAGE_BUILDER.DESIGNER.HISTORY.CONFIRM", { version: label })))) return;
  try {
    if (designer.file.value) await designer.restore(version.sha);
    else {
      await restoreGroupVersion(designer.groupId.value!, version.sha);
      await reopen();
    }
    notification.success(t("PAGE_BUILDER.DESIGNER.HISTORY.RESTORED"));
  } catch {
    notification.error(t("PAGE_BUILDER.DESIGNER.HISTORY.RESTORE_FAILED"));
  }
}

// saved drafts to throw away: a header or footer draft, or the draft of a published page (one never published has
// nothing to go back to); else only the unsaved edits go, back to the last save
const hasDraft = computed(
  () => designer.groupDrafts.value.length > 0 || (!!designer.status.value.hasChanges && !!designer.status.value.published),
);

async function discardChanges() {
  const drafts = hasDraft.value;
  if (!(await showConfirmation(t(drafts ? "PAGE_BUILDER.DESIGNER.HISTORY.CONFIRM_DISCARD" : "PAGE_BUILDER.DESIGNER.CONFIRM_DISCARD_UNSAVED")))) return;
  try {
    if (drafts) await designer.discardDrafts();
    theme.revert();
    await reopen();
    if (drafts) notification.success(t("PAGE_BUILDER.DESIGNER.HISTORY.DISCARDED"));
  } catch {
    notification.error(t("PAGE_BUILDER.DESIGNER.HISTORY.DISCARD_FAILED"));
  }
}

const publishTitle = computed(() => {
  const status = designer.status.value;
  if (status.pending && !status.awaitingMerge) return "PAGE_BUILDER.DESIGNER.PUBLISHING";
  return status.awaitingMerge && status.hasChanges ? "PAGE_BUILDER.DESIGNER.RETRY_PUBLISH" : "PAGE_BUILDER.DESIGNER.PUBLISH";
});

/**
 * Opens the site's history panel next to the designer, which makes room for it meanwhile. Once history is on there,
 * a database page has it from its next save; a file page is opened again from git (unless it has unsaved edits).
 */
const sitePanel = ref(false);
function enableHistory() {
  const storeId = designer.storeId.value;
  if (!storeId) return;
  const file = designer.file.value;
  fullView.value = false;
  sitePanel.value = true;
  if (maximized) maximized.value = false;
  openBlade({
    name: "PageBuilderGit",
    param: storeId,
    onClose: async () => {
      sitePanel.value = false;
      if (maximized) maximized.value = true;
      const on = await isGitFlow(storeId, file?.theme ?? (await storeTheme(storeId)));
      historyOn.value = on;
      if (!on || !file) return;
      // unsaved edits stay on screen; their save goes to git now, and so does publishing
      if (unsaved.value) designer.gitFlow.value = true;
      else await designer.open(file);
    },
  });
}

const toolbar = computed<IBladeToolbar[]>(() => [
  {
    id: "save",
    title: t(designer.busy.value === "saving" ? "PAGE_BUILDER.DESIGNER.SAVING" : "PAGE_BUILDER.DESIGNER.SAVE"),
    icon: "lucide-save",
    disabled: !unsaved.value || !!designer.busy.value,
    shortcut: hotkey.mod.s,
    clickHandler: save,
  },
  {
    id: "undo",
    title: t("PAGE_BUILDER.DESIGNER.UNDO"),
    icon: "lucide-undo-2",
    disabled: !designer.canUndo.value,
    clickHandler: designer.undo,
  },
  {
    id: "redo",
    title: t("PAGE_BUILDER.DESIGNER.REDO"),
    icon: "lucide-redo-2",
    disabled: !designer.canRedo.value,
    clickHandler: designer.redo,
  },
  {
    id: "publish",
    title: t(publishTitle.value),
    icon: "lucide-upload",
    isVisible: !designer.shared.value,
    disabled: !designer.canPublish.value,
    clickHandler: () => publish(true),
  },
  {
    id: "unpublish",
    title: t("PAGE_BUILDER.DESIGNER.UNPUBLISH"),
    icon: "lucide-cloud-off",
    isVisible: !!designer.status.value.published,
    disabled: !designer.canUnpublish.value,
    clickHandler: () => publish(false),
  },
  {
    // the saved draft and the unsaved edits, from here (as from the version history)
    id: "discard",
    title: t("PAGE_BUILDER.DESIGNER.HISTORY.DISCARD"),
    icon: "lucide-rotate-ccw",
    isVisible: !designer.shared.value && (unsaved.value || hasDraft.value),
    disabled: !!designer.busy.value,
    clickHandler: discardChanges,
  },
  {
    id: "promote",
    title: t("PAGE_BUILDER.DESIGNER.PROMOTE"),
    icon: "lucide-rocket",
    // only with a production branch configured (git)
    isVisible: !!designer.status.value.production,
    disabled: !designer.canPromote.value,
    clickHandler: promote,
  },
  {
    id: "preview",
    title: t("PAGE_BUILDER.DESIGNER.OPEN_PREVIEW"),
    icon: "lucide-external-link",
    isVisible: !designer.shared.value,
    disabled: designer.dirty.value || (!storefront.value?.url && !designer.gitFlow.value),
    clickHandler: openPreview,
  },
  {
    id: "history",
    title: t("PAGE_BUILDER.DESIGNER.HISTORY.TITLE"),
    icon: "lucide-history",
    isVisible: historyAvailable.value,
    clickHandler: () => (historyOpen.value ? closeHistory() : openHistory()),
  },
  {
    // a file page of a site without history: the site's history panel, where it is switched on
    id: "enable-history",
    title: t("PAGE_BUILDER.GIT.TURN_ON"),
    icon: "lucide-history",
    isVisible: !historyAvailable.value && (!!designer.file.value || !!designer.groupId.value),
    clickHandler: enableHistory,
  },
  {
    id: "full-view",
    title: t(fullView.value ? "PAGE_BUILDER.DESIGNER.EXIT_FULL_VIEW" : "PAGE_BUILDER.DESIGNER.FULL_VIEW"),
    icon: fullView.value ? "lucide-minimize-2" : "lucide-maximize-2",
    clickHandler: () => (fullView.value = !fullView.value),
  },
]);

// full view's bar: the toolbar's actions, small; Publish and Save with their names, the others as icons
const barActions = computed(() =>
  toolbar.value
    .filter((item) => item.isVisible !== false && item.id !== "full-view")
    .map((item) => {
      const named = item.id === "publish" || item.id === "save";
      let variant: "primary" | "secondary" | "ghost" = "ghost";
      if (item.id === "save") variant = "primary";
      else if (item.id === "publish") variant = "secondary";
      return { ...item, variant, label: named ? item.title : undefined };
    }),
);

// unsaved edits: ask before the blade closes or the tab goes away
useBeforeUnload(unsaved);
// true keeps the blade open: only with unsaved edits the user chose to keep
onBeforeClose(async () => unsaved.value && !(await showConfirmation(t("PAGE_BUILDER.DESIGNER.DISCARD"))));

const { getApiClient } = useApiClient(PageBuilderPageClient);
onMounted(async () => {
  let storeId: string;
  let open: Promise<void>;
  if (param.value) {
    group.value = await (await getApiClient()).getGroup(param.value);
    if (archived.value) return;
    storeId = group.value.storeId!;
    open = designer.open({ groupId: param.value, storeId });
  } else if (query.value?.storeId && query.value.sharedComponentId) {
    storeId = query.value.storeId;
    open = designer.open({ sharedComponentId: query.value.sharedComponentId, storeId });
  } else if (query.value?.storeId && query.value.path) {
    storeId = query.value.storeId;
    const page: FilePage = { storeId, path: query.value.path, type: query.value.type || "pages", theme: await storeTheme(storeId) };
    open = designer.open(page);
  } else {
    return;
  }
  // the assets library (the picker) works on this store
  useUrlParams().setStoreId(storeId);
  await Promise.all([
    open,
    storeTheme(storeId).then((name) =>
      Promise.all([theme.load(storeId, name), param.value ? isGitFlow(storeId, name).then((on) => (historyOn.value = on)) : undefined]),
    ),
    loadStorefront(storeId).then((x) => {
      storefront.value = x;
      url.value = previewUrl(x.url, x.previewPath);
      storefrontChecked.value = true;
    }),
  ]);
});
</script>

<style lang="scss" scoped>
// full view: the bar in the designer replaces the blade's header and toolbar (on phones the header stays: no bar there)
@media (min-width: 768px) {
  .pd-full-view :deep(.vc-blade__header),
  .pd-full-view :deep(.vc-blade__toolbar) {
    display: none;
  }

  // the admin shell's environment ribbon holds the top-right corner: the bar keeps clear of it, as the shell's header does
  :global(.vc-app__main-content:has(> .as-ribbon) .pd--full-view .pd__canvas-bar) {
    padding-right: 112px;
  }

  :global(.vc-app__main-content:has(> .as-ribbon) .pd--full-view .pd-settings__head) {
    padding-right: 64px;
  }
}

.pd {
  display: flex;
  height: 100%;
  min-height: 0;
  background: var(--neutrals-50);

  &__left,
  &__right {
    flex: none;
    width: 300px;
    min-height: 0;
    overflow-y: auto;
    background: var(--additional-50);
  }

  &__left {
    border-right: 1px solid var(--neutrals-200);
  }

  &__notice {
    display: flex;
    align-items: flex-start;
    gap: 6px;
    margin: 8px 8px 0;
    padding: 8px 10px;
    border-radius: 6px;
    background: var(--primary-50);
    color: var(--primary-700);
    font-size: 12px;
    line-height: 16px;
  }

  &__modes {
    display: flex;
    gap: 4px;
    padding: 8px 8px 0;
  }

  &__mode {
    flex: 1;
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 6px;
    height: 32px;
    border: 0;
    border-radius: 6px;
    background: none;
    color: var(--neutrals-500);
    font: inherit;
    font-size: 13px;
    cursor: pointer;

    &:hover {
      background: var(--neutrals-100);
    }

    &--on,
    &--on:hover {
      background: var(--primary-50);
      color: var(--primary-700);
      font-weight: 600;
    }
  }

  &__right {
    border-left: 1px solid var(--neutrals-200);
    overflow: hidden;
  }

  &--fullscreen &__left,
  &--fullscreen &__right {
    display: none;
  }

  // narrower screens: slimmer panels; a phone: the panels only, one above the other (no room for a preview)
  @media (max-width: 1280px) {
    &__left,
    &__right {
      width: 260px;
    }
  }

  @media (max-width: 767px) {
    flex-direction: column;
    overflow-y: auto;

    &__left,
    &__right {
      width: auto;
      overflow: visible;
      border: 0;
      border-bottom: 1px solid var(--neutrals-200);
    }

    &__canvas {
      display: none;
    }
  }

  &__canvas {
    position: relative;
    flex: 1;
    min-width: 0;
    display: flex;
    flex-direction: column;
  }

  // full view: the canvas bar spans the window over the panels, with the page's name and actions in it
  @media (min-width: 768px) {
    &--full-view {
      display: grid;
      grid-template: "bar bar bar" auto "left stage right" minmax(0, 1fr) / auto minmax(0, 1fr) auto;
    }

    &--full-view &__canvas {
      display: contents;
    }

    &--full-view &__canvas-bar {
      grid-area: bar;
      padding: 6px 8px;
      border-bottom: 1px solid var(--neutrals-200);
      background: var(--additional-50);
    }

    &--full-view &__left {
      grid-area: left;
    }

    &--full-view &__stage {
      grid-area: stage;
      padding-top: 12px;
    }

    &--full-view &__right {
      grid-area: right;
    }
  }

  &__crumbs {
    display: flex;
    align-items: center;
    gap: 2px;
    min-width: 0;
    white-space: nowrap;
  }

  &__crumb {
    padding: 2px 4px;
    border: 0;
    border-radius: 4px;
    background: none;
    color: var(--neutrals-500);
    font: inherit;
    font-size: 13px;
    cursor: pointer;

    &:hover {
      background: var(--neutrals-100);
      color: var(--neutrals-900);
    }
  }

  &__crumb-sep {
    flex: none;
    color: var(--neutrals-400);
  }

  &__name {
    display: flex;
    align-items: baseline;
    gap: 8px;
    min-width: 0;
    white-space: nowrap;

    strong {
      font-size: 14px;
      font-weight: 600;
    }

    small {
      overflow: hidden;
      text-overflow: ellipsis;
      color: var(--neutrals-500);
      font-size: 12px;
    }
  }

  &__actions {
    display: flex;
    align-items: center;
    gap: 4px;
    margin-left: 8px;
    padding-left: 12px;
    border-left: 1px solid var(--neutrals-200);
  }

  &__canvas-bar {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 8px 16px;

    // a version being viewed: its chip gives way first, the buttons keep their one line
    :deep(.vc-button) {
      flex: none;
      white-space: nowrap;
    }
  }

  &__spacer {
    flex: 1;
  }

  &__status {
    padding: 2px 10px;
    border-radius: 999px;
    font-size: 12px;
    font-weight: 500;
    background: var(--neutrals-100);
    color: var(--neutrals-700);

    &--published {
      background: var(--success-50);
      color: var(--success-700);
    }

    &--shared {
      background: var(--primary-50);
      color: var(--primary-700);
    }

    &--viewing {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      min-width: 0;
      overflow: hidden;
      white-space: nowrap;
      text-overflow: ellipsis;
      background: var(--primary-50);
      color: var(--primary-700);
    }

    &--pending {
      background: var(--info-50);
      color: var(--info-700);
    }

    &--changes,
    &--awaiting,
    &--unsaved {
      background: var(--warning-50);
      color: var(--warning-700);
    }
  }

  &__inspector {
    display: flex;
    margin-right: 8px;
    padding: 6px 9px;
    border: 1px solid var(--neutrals-200);
    border-radius: 8px;
    background: var(--additional-50);
    color: var(--neutrals-500);
    cursor: pointer;

    &--on {
      border-color: var(--primary-200);
      background: var(--primary-50);
      color: var(--primary-700);
    }
  }

  &__devices {
    display: flex;
    padding: 2px;
    border: 1px solid var(--neutrals-200);
    border-radius: 8px;
    background: var(--additional-50);
  }

  &__device {
    display: flex;
    padding: 5px 8px;
    border: 0;
    border-radius: 6px;
    background: none;
    color: var(--neutrals-500);
    cursor: pointer;

    &--on {
      background: var(--primary-50);
      color: var(--primary-700);
    }
  }

  &__stage {
    flex: 1;
    min-height: 0;
    display: flex;
    justify-content: center;
    padding: 0 16px 16px;
  }

  &__frame {
    position: relative;
    width: 100%;
    height: 100%;
    border-radius: 8px;
    overflow: hidden;
    background: #fff;
    box-shadow: 0 0 0 1px var(--neutrals-200);
    transition: width 0.2s;

    &--tablet {
      width: 48rem;
    }

    &--phone {
      width: 24rem;
    }

    iframe {
      display: block;
      width: 100%;
      height: 100%;
      border: 0;
    }
  }
}

.pd-dialog {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 12px;
  width: min(560px, 80vw);

  p {
    margin: 0;
    color: var(--neutrals-600);
  }

  > :deep(*) {
    align-self: stretch;
  }

  > :last-child {
    align-self: flex-end;
  }

  &__code :deep(textarea) {
    min-height: 200px;
    font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
    font-size: 12px;
  }
}

.pd-picker__body {
  height: min(70vh, 720px);
}

.pd__frame-wait {
  position: absolute;
  inset: 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 12px;
  padding: 24px;
  background: var(--neutrals-50);
  color: var(--neutrals-500);
  font-size: 13px;
  text-align: center;

  p {
    max-width: 420px;
    margin: 0;
  }
}

.pd__spin {
  animation: pd-spin 1s linear infinite;
}

@keyframes pd-spin {
  to {
    transform: rotate(360deg);
  }
}

.pd-message {
  height: 100%;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 12px;
  padding: 24px;
  text-align: center;
  color: var(--neutrals-500);

  p {
    max-width: 420px;
    margin: 0;
  }
}
</style>
