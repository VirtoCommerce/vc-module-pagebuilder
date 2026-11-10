<template>
  <VcBlade
    :width="sitePanel ? '60%' : '100%'"
    :class="{ 'pd-full-view': fullViewOn, 'pd-own-actions': ownActions }"
    :title="title"
    :subtitle="subtitle"
    :toolbar-items="toolbar"
    :modified="unsaved"
    :loading="(!!param && !group && !openFailed) || designer.busy.value === 'loading' || designer.busy.value === 'restoring'"
  >
    <div
      v-if="!designerShown"
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
        <p
          v-else-if="designer.templateIsNew.value"
          class="pd__notice"
        >
          <VcIcon
            icon="lucide-info"
            size="s"
          />
          {{ $t("PAGE_BUILDER.DESIGNER.TEMPLATES.NEW") }}
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
          :look="look"
          :store-id="designer.storeId.value"
          :palette-tokens="designer.handshake.value?.paletteTokens"
          :overridden="whiteLabeled"
        />
        <SharedPanel
          v-if="mode === 'shared'"
          ref="sharedPanel"
          :store-id="designer.storeId.value"
          :can-insert="designer.canPlaceShared.value"
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
            <span
              v-if="!switcherShown"
              class="pd__name"
            >
              <strong>{{ title }}</strong>
              <small>{{ subtitle }}</small>
            </span>
          </template>
          <TemplateSwitcher
            v-if="switcherShown"
            :current="designer.templateKey.value ?? null"
            :page="homeTitle"
            :keys="templateKeyList"
            :rule-count="(key) => rulesFor(rules.rules.value, key).length"
            :disabled="!!designer.busy.value"
            @open="switchTo"
            @create="startCreate"
          />
          <EntityPicker
            v-if="designer.pageType.value && catalog"
            :model-value="previewed?.id"
            :type="designer.pageType.value"
            :catalog="catalog"
            @picked="pickEntity"
          />
          <span
            v-if="inEffect"
            class="pd__status"
            :class="inEffect.other ? 'pd__status--changes' : 'pd__status--pending'"
            :title="inEffect.title"
          >
            {{ inEffect.text }}
          </span>
          <template v-if="previewing">
            <span
              class="pd__status pd__status--viewing"
              :title="previewing.description"
            >
              <VcIcon
                icon="lucide-history"
                size="xs"
              />
              {{
                $t("PAGE_BUILDER.DESIGNER.HISTORY.VIEWING", {
                  version: previewing.label,
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
          <VcDropdown
            v-if="languageSwitch"
            :model-value="menu === 'language'"
            :items="storeLanguages"
            :item-text="(x: string) => x"
            :is-item-active="(x: string) => x === editLanguage"
            floating
            placement="bottom-end"
            @update:model-value="(open: boolean) => (menu = open ? 'language' : undefined)"
            @item-click="pickLanguage"
          >
            <template #trigger="{ isActive, toggle }">
              <button
                type="button"
                class="pd__inspector pd__picker"
                :aria-expanded="isActive"
                :title="$t('PAGE_BUILDER.DESIGNER.TRANSLATION.LANGUAGE')"
                :aria-label="$t('PAGE_BUILDER.DESIGNER.TRANSLATION.LANGUAGE')"
                @click="toggle"
              >
                <VcIcon
                  icon="lucide-languages"
                  size="s"
                />
                {{ editLanguage }}
              </button>
            </template>
          </VcDropdown>
          <VcDropdown
            v-if="previewAs.allowed.value && previewAs.users.value.length"
            :model-value="menu === 'user'"
            :items="[{ id: null, name: $t('PAGE_BUILDER.DESIGNER.PREVIEW_AS.ANONYMOUS') }, ...previewAs.users.value]"
            :item-text="(x: { name: string }) => x.name"
            :is-item-active="(x: { id: string | null }) => x.id === previewAs.userId.value"
            floating
            placement="bottom-end"
            @update:model-value="(open: boolean) => (menu = open ? 'user' : undefined)"
            @item-click="(x: { id: string | null }) => previewUser(x.id)"
          >
            <template #trigger="{ isActive, toggle }">
              <button
                type="button"
                class="pd__inspector pd__picker"
                :class="{ 'pd__inspector--on': !!previewAs.userId.value }"
                :aria-expanded="isActive"
                :title="previewAs.userId.value ? $t('PAGE_BUILDER.DESIGNER.PREVIEW_AS.CLICKS_BLOCKED') : $t('PAGE_BUILDER.DESIGNER.PREVIEW_AS.TITLE')"
                :aria-label="$t('PAGE_BUILDER.DESIGNER.PREVIEW_AS.TITLE')"
                @click="toggle"
              >
                <VcIcon
                  :icon="previewAs.userId.value ? 'lucide-user-round-check' : 'lucide-user-round'"
                  size="s"
                />
                <span class="pd__picker-name">{{ previewAs.user.value?.name ?? $t("PAGE_BUILDER.DESIGNER.PREVIEW_AS.ANONYMOUS") }}</span>
              </button>
            </template>
          </VcDropdown>
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
            v-if="designer.can('color-scheme')"
            role="radiogroup"
            class="pd__devices pd__schemes"
            :aria-label="$t('PAGE_BUILDER.DESIGNER.COLOR_SCHEME')"
          >
            <button
              v-for="s in COLOR_SCHEMES"
              :key="s.id"
              type="button"
              role="radio"
              class="pd__device"
              :class="{ 'pd__device--on': colorScheme === s.id }"
              :aria-checked="colorScheme === s.id"
              :title="$t(s.label)"
              :aria-label="$t(s.label)"
              @click="colorScheme = s.id"
            >
              <VcIcon
                :icon="s.icon"
                size="s"
              />
            </button>
          </div>
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
            v-if="ownActions"
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
            <!-- credentialless: the platform's pages require embedded documents to opt in to COEP, and an anonymous frame needs no opt-in from the storefront. -->
            <iframe
              v-if="url"
              ref="frame"
              credentialless
              :src="url"
              :title="$t('PAGE_BUILDER.DESIGNER.PREVIEW')"
              @load="bridge.onFrameLoad"
            />
            <div
              v-if="url && bridge.unsupported.value"
              class="pd__frame-wait"
              role="alert"
            >
              <VcIcon
                icon="lucide-monitor-off"
                size="xl"
              />
              <p>{{ $t("PAGE_BUILDER.DESIGNER.PREVIEW_UNSUPPORTED") }}</p>
            </div>
            <div
              v-else-if="url && !bridge.connected.value"
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
          :describe="describeVersion"
          :previewable="previewableVersion"
          :restore-refused="restoreRefused"
          :previewing="previewing?.id"
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
        v-else-if="rulesOpen && designer.templateKey.value && catalog && themeName"
        class="pd__right"
      >
        <RulesPanel
          :rules="rules"
          :type="designer.pageType.value!"
          :template-key="designer.templateKey.value"
          :keys="templateKeyList"
          :catalog="catalog"
          :theme="themeName"
          :previewed="previewed"
          :picked="rulesChoice"
          :conditions="designer.handshake.value?.conditions"
          :built-in-templates="designer.handshake.value?.builtInTemplates"
          :readonly="!mayEditTemplates"
          @close="rulesOpen = false"
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
            :disabled="!sharedName?.trim() || savingShared"
            @click="saveShared"
          >
            {{ $t("PAGE_BUILDER.DESIGNER.SAVE") }}
          </VcButton>
        </div>
      </template>
    </VcPopup>


    <DiscardDialog
      v-if="discarding"
      :options="discarding"
      :template="designer.themeFileOpen.value"
      :unsaved="unsaved"
      @discard="discardDrafts"
      @close="discarding = undefined"
    />

    <VcPopup
      v-if="creating"
      :model-value="true"
      :title="$t('PAGE_BUILDER.DESIGNER.TEMPLATES.CREATE_TITLE')"
      @close="creating = undefined"
    >
      <template #content>
        <div class="pd-dialog pd-dialog--fit">
          <p>{{ $t("PAGE_BUILDER.DESIGNER.TEMPLATES.CREATE_HINT") }}</p>
          <VcInput
            v-model="creating.name"
            :label="$t('PAGE_BUILDER.DESIGNER.TEMPLATES.NAME')"
            :hint="newKey ? $t('PAGE_BUILDER.DESIGNER.TEMPLATES.SAVED_AS', { path: templatePath(themeName ?? '', newKey) }) : undefined"
            :error="newKeyTaken"
            :error-message="newKeyTaken ? $t('PAGE_BUILDER.DESIGNER.TEMPLATES.EXISTS') : undefined"
            :maxlength="64"
            required
            @keydown.enter="createTemplate"
          />
          <VcButton
            :disabled="!newKey || newKeyTaken || creatingNow || !!designer.busy.value"
            @click="createTemplate"
          >
            {{ $t("PAGE_BUILDER.DESIGNER.TEMPLATES.CREATE_BUTTON") }}
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
            :accept="pickTypes"
            @pick="picked"
          />
        </div>
      </template>
    </VcPopup>
  </VcBlade>
</template>

<script lang="ts" setup>
import { computed, h, inject, onBeforeUnmount, onMounted, provide, ref, shallowRef, watch, watchEffect } from "vue";
import { useEventListener, useLocalStorage, useMediaQuery } from "@vueuse/core";
import { useI18n } from "vue-i18n";
import { useRouter } from "vue-router";
import { debounce } from "lodash-es";
import {
  BladeMaximizedKey,
  BladeStackKey,
  hotkey,
  notification,
  useApiClient,
  useBeforeUnload,
  useBlade,
  useBladeStack,
  usePermissions,
  usePopup,
  useResponsive,
  type BladeDescriptor,
  type IBladeToolbar,
} from "@vc-shell/framework";
import { VcBlade, VcButton, VcDropdown, VcIcon, VcInput, VcPopup, VcTextarea } from "@vc-shell/framework/ui";
import { fromClipboard, toClipboard } from "../designer/clipboard";
import { PageBuilderPageClient, type GroupedPageBuilderPage } from "../../api_client/virtocommerce.pagebuildermodule";
import VersionHistory from "../designer/components/VersionHistory.vue";
import { notifyPagesChanged, useUrlParams } from "../page-builder/composables";
import AssetsLibraryContent from "../asset-library/components/AssetsLibraryContent.vue";
import { PageAnchorsKey, PaletteTokensKey, PickAssetKey, StoreMenusKey, WhiteLabelingKey } from "../designer/types";
import { pageAnchors } from "../designer/anchors";
import { useDesigner } from "../designer/useDesigner";
import { resolveRefs, toDocument } from "../designer/document";
import { pageOnly } from "../designer/groups";
import { previewUrl, storefrontPage, usePreviewBridge } from "../designer/preview";
import {
  COLOR_SCHEMES,
  draftMessage,
  ensureCatalog,
  handshakeMessages,
  renderMessage,
  rulesPick,
  selectionFrom,
  type ColorScheme,
  type DraftMessage,
  type PageSubject,
  type RenderInput,
  type RenderMessage,
  type Resolved,
  type RulesPick,
} from "../designer/previewMessages";
import {
  loadStorefront,
  storeTheme,
  filePreviewUrl,
  gitFlowOf,
  fileHistory,
  historyStatus,
  contentHistory,
  contentVersion,
  restoreContentVersion,
  saveDesigner,
  storeMenus,
  filePageContent,
  hasCatalog,
  uploadCatalog,
  ApiError, type FilePage, type SharedComponent } from "../designer/api";
import { newItem } from "../designer/schema";
import {
  searchCategories,
  searchPages,
  searchProducts,
  templateKeys,
  findFilePage,
  findTemplate,
  loadThemeFile,
  type CatalogContext,
  type NamedEntity,
} from "../designer/api";
import { PAGE_CONTENT, alternateKey, alternateName, entityUrl, pageTypeOf, startingTemplate, templateEntry, templatePath, type PageType } from "../designer/templates";
import { rulesFor } from "../designer/rules";
import { useTemplateRules } from "../designer/templateRules";
import {
  THEME_ONLY_FILES,
  fromGitVersion,
  historyItems,
  historyOffer,
  partLabel,
  type HistoryFile,
  type HistoryStatus,
  type HistoryVersion,
  type ThemePart,
} from "../designer/history";
import { publishActions, themeErrors, themePublishButton } from "../designer/themePublication";
import { CatalogPendingError } from "../designer/designerSave";
import type { Field, Item, PageDocument, SectionGroup, Selection } from "../designer/types";
import { draftTemplates, savedTemplatesReader } from "../designer/templateDrafts";
import TemplateSwitcher from "../designer/components/TemplateSwitcher.vue";
import EntityPicker from "../designer/components/EntityPicker.vue";
import RulesPanel from "../designer/components/RulesPanel.vue";
import SectionTree from "../designer/components/SectionTree.vue";
import SettingsPanel from "../designer/components/SettingsPanel.vue";
import ThemePanel from "../designer/components/ThemePanel.vue";
import SharedPanel from "../designer/components/SharedPanel.vue";
import { useSharedComponentDesigner } from "../page-builder/composables/usePageDesigner";
import { useThemeLook } from "../designer/useThemeLook";
import { usePreviewUser } from "../designer/usePreviewUser";
import { LOOK_FILES } from "../designer/themeLook";
import { PERMISSIONS, refusedParts } from "../designer/permissions";
import { discardOptions, type DiscardOption } from "../designer/discard";
import DiscardDialog from "../designer/components/DiscardDialog.vue";

defineBlade({
  name: "PageBuilderDesigner",
  url: "/page-builder-designer",
});

const { t } = useI18n({ useScope: "global" });
const { id: bladeId, param, query, onBeforeClose, openBlade } = useBlade();
const { showConfirmation } = usePopup();
const designer = useDesigner();
// The server checks the same permissions on Save and Discard.
const { hasAccess } = usePermissions();
const mayEditTheme = hasAccess(PERMISSIONS.theme);
const mayEditTemplates = hasAccess(PERMISSIONS.templates);
designer.groupsEditable.value = mayEditTheme;
const look = useThemeLook();
const modes = [
  { id: "sections", icon: "lucide-layout-list", label: "PAGE_BUILDER.DESIGNER.SECTIONS" },
  { id: "theme", icon: "lucide-palette", label: "PAGE_BUILDER.DESIGNER.THEME.TITLE" },
  { id: "shared", icon: "lucide-link", label: "PAGE_BUILDER.DESIGNER.MODE_SHARED" },
] as const;
const mode = ref<(typeof modes)[number]["id"]>("sections");
// A shared component can't hold another one, and its Save covers only the component, so it has no Shared or Theme tab.
const shownModes = computed(() => modes.filter((m) => (m.id !== "shared" && m.id !== "theme") || (!designer.shared.value && (m.id !== "theme" || mayEditTheme))));
const rules = useTemplateRules();
const rulesActive = computed(() => !!designer.templateKey.value && rules.loaded.value);
const unsaved = computed(() => designer.dirty.value || look.dirty.value || rules.dirty.value);

const maximized = inject(BladeMaximizedKey, null);
if (maximized) maximized.value = true;
// The full-view z-index sits over the shell's chrome (40-60) and under its dropdowns (100+) and popups (9000+).
const fullView = useLocalStorage("vc-page-designer-full-view", false);
const designerShown = computed(() => !designer.loadError.value && !archived.value && !openFailed.value);
// Full view hides the blade's header, and only the designer's own bar has a way out of it.
const fullViewOn = computed(() => fullView.value && designerShown.value);
// The shell's narrow mode folds a blade's toolbar into a floating button, so the designer's bar carries the actions while the preview shows.
const { isMobile } = useResponsive();
const previewShown = useMediaQuery("(min-width: 768px)");
const ownActions = computed(() => designerShown.value && (fullView.value || (isMobile.value && previewShown.value)));
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
  // VcBlade re-renders its own inline width and the shells' maximized widths are !important, so only min-width works.
  "min-width": "100%",
  margin: "0",
  "border-radius": "0",
  "box-shadow": "none",
};

const group = ref<GroupedPageBuilderPage>();
const fileName = computed(() => designer.file.value?.path.split("/").pop()?.replace(/\.page(-draft)?$/, ""));
const TYPE_TITLES: Record<PageType, string> = {
  product: "PAGE_BUILDER.DESIGNER.TEMPLATES.PRODUCT_TITLE",
  category: "PAGE_BUILDER.DESIGNER.TEMPLATES.CATEGORY_TITLE",
  page: "PAGE_BUILDER.DESIGNER.TEMPLATES.PAGE_TITLE",
};
const templateName = (key: string) => (key.includes(".") ? alternateName(key) : t("PAGE_BUILDER.DESIGNER.TEMPLATES.DEFAULT"));
const homeTitle = ref<string>();
const title = computed(() => {
  const key = designer.templateKey.value;
  if (key) return t(TYPE_TITLES[pageTypeOf(key)!], { name: templateName(key) });
  return designer.shared.value?.name ?? group.value?.name ?? ((designer.doc.value.settings.name as string | undefined) || fileName.value || "");
});
const cultureName = computed(() => group.value?.cultureName ?? query.value?.cultureName);
const archived = computed(() => group.value?.status === "Archived");

type StatusKind = "unsaved" | "draft" | "published" | "changes" | "pending" | "awaiting" | "behind" | "shared";
const statusKind = computed<StatusKind>(() => {
  if (unsaved.value) return "unsaved";
  // A shared component isn't published on its own: it shows wherever a page places it.
  if (designer.shared.value) return "shared";
  const status = designer.status.value;
  if (status.awaitingMerge) return "awaiting";
  if (status.pending) return "pending";
  // Git: a merge done on GitHub left the live page as it was.
  if (status.liveBehind) return "behind";
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
const openFailed = ref(false);

const storeLanguages = ref<string[]>([]);
const pickedLanguage = ref<string>();
const languageLocked = computed(() => !!cultureName.value && !designer.templateKey.value);
const editLanguage = computed(() => (languageLocked.value ? cultureName.value : pickedLanguage.value) || cultureName.value || catalog.value?.cultureName || "en-US");
const languageSwitch = computed(
  () => storeLanguages.value.length > 1 && !languageLocked.value && !designer.shared.value && (!!designer.templateKey.value || designer.groupsShown.value),
);
const menu = ref<"language" | "user">();
function pickLanguage(language: string) {
  menu.value = undefined;
  pickedLanguage.value = language;
}
function previewUser(id: string | null) {
  menu.value = undefined;
  void previewAs.pick(id);
}
const pageSubject = computed<PageSubject | undefined>(() => {
  if (designer.templateKey.value || designer.shared.value) return undefined;
  if (designer.groupId.value) {
    const permalink = group.value?.permalink?.trim();
    return permalink ? { permalink: `/${permalink.replace(/^\/+/, "")}`, objectType: "Pages" } : undefined;
  }
  const file = designer.file.value;
  const permalink = designer.doc.value.settings.permalink;
  return file && file.type !== "themes" && typeof permalink === "string" && permalink.trim() ? { permalink, objectType: "ContentFile" } : undefined;
});

// The preview keeps nothing of the previous render, so each message carries everything it needs.
function render(options: { scroll?: boolean; model?: Item; preview?: RenderInput["preview"]; selection?: Selection | null } = {}): RenderMessage {
  const templateKey = designer.templateKey.value;
  return renderMessage({
    preview: options.preview ?? designer.preview.value,
    cultureName: editLanguage.value,
    templateKey,
    url: templateKey ? previewUrlOf.value : undefined,
    page: pageSubject.value,
    selection: options.selection === undefined ? designer.selection.value : options.selection,
    scroll: options.scroll,
    model: options.model,
    sectionLabels: designer.sectionLabels.value,
  });
}

// The open template is in the draft and in the render, and the render's copy wins.
function draft(groups = designer.groupsPreview.value): DraftMessage {
  const templateKey = designer.templateKey.value;
  return draftMessage({
    rules: rules.loaded.value ? rules.rules.value : undefined,
    templates: designer.shared.value
      ? undefined
      : draftTemplates(savedTemplates.value, templateKey ? { key: templateKey, document: designer.preview.value.template } : undefined),
    groups,
    look: look.draft.value,
    capabilities: designer.handshake.value?.capabilities ?? [],
  });
}

const savedTemplates = shallowRef<Record<string, PageDocument>>();

const colorScheme = ref<ColorScheme>("light");

let versionView: { draft: DraftMessage; render: RenderMessage } | undefined;

const previewAs = usePreviewUser({
  send: (message) => bridge.send(message),
  onFailed: (user) => notification.error(t("PAGE_BUILDER.DESIGNER.PREVIEW_AS.FAILED", { name: user?.name ?? "" })),
});

const bridge = usePreviewBridge({
  frame,
  url,
  onLoaded: (loaded) => {
    // A new document reports what its rules picked again.
    reported.value = undefined;
    designer.handshake.value = {
      hash: loaded.hash,
      capabilities: loaded.capabilities,
      conditions: loaded.conditions,
      builtInTemplates: loaded.builtInTemplates,
      paletteTokens: loaded.paletteTokens,
    };
    designer.schemas.value = {
      ...loaded.catalog,
      sections: { [PAGE_CONTENT]: { name: t("PAGE_BUILDER.DESIGNER.TEMPLATES.PAGE_CONTENT"), icon: "lucide-file-text", targetTemplates: ["page", "page-template"] }, ...loaded.catalog.sections },
    };
  },
  // Saves, publications and agents check the theme against the catalog of the storefront previewed.
  prepare: async (loaded) => {
    await Promise.all([
      designer.storeId.value ? ensureCatalog(designer.storeId.value, loaded.catalog, { has: hasCatalog, upload: uploadCatalog }) : undefined,
      previewAs.refresh(),
    ]);
  },
  // A reloaded frame has lost everything: whom it renders as, the inspector, light or dark, the theme, the page.
  handshake: () =>
    handshakeMessages({
      auth: previewAs.message(),
      inspector: !versionView && inspector.value,
      colorScheme: colorScheme.value,
      draft: versionView?.draft ?? draft(),
      render: versionView?.render ?? render({ scroll: true }),
    }),
  onSelect: (event) => {
    if (designer.isLocked(event.sectionId)) return refuseLocked();
    const next = selectionFrom(designer.doc.value, event);
    if (JSON.stringify(next) === JSON.stringify(designer.selection.value)) return;
    selectedInPreview = true;
    designer.selection.value = next;
  },
  onHover: (id) => (designer.hovered.value = id),
  onResolved: (value) => (reported.value = value),
  onEdit: ({ itemId, path, value, cultureName: language }) => {
    // A version shown is look-only: what is typed there is not the page's.
    if (versionView) return;
    if (lockedItem(itemId)) return refuseLocked();
    const target = designer.setText(itemId, path, value, language);
    if (!target) return answerRefusedEdit();
    if (JSON.stringify(target) === JSON.stringify(designer.selection.value)) return;
    selectedInPreview = true;
    designer.selection.value = target;
  },
});

const lockedItem = (itemId: string) =>
  designer.doc.value.content.some((x) => (x.id === itemId || !!x.blocks?.some((b) => b.id === itemId)) && designer.isLocked(x.id));
let lockedSaid = false;
// The preview keeps a typed text until the edit is answered: the old values tell it to take the text back.
function answerRefusedEdit() {
  sendDraft();
  sendRender();
}
function refuseLocked() {
  answerRefusedEdit();
  if (lockedSaid) return;
  lockedSaid = true;
  notification.warning(t("PAGE_BUILDER.DESIGNER.GROUPS.LOCKED"));
}

const draftJson = computed(() => JSON.stringify(draft().theme));
const sendDraft = debounce(() => {
  if (!versionView) bridge.send(draft());
}, 120);
watch(draftJson, sendDraft);

// A storefront that isn't running never answers the handshake, so after this long the canvas says so.
const PREVIEW_SLOW_MS = 20000;
const previewSlow = ref(false);
let previewTimer: ReturnType<typeof setTimeout> | undefined;
function waitForPreview() {
  clearTimeout(previewTimer);
  previewSlow.value = false;
  if (!bridge.connected.value && !bridge.unsupported.value && url.value) previewTimer = setTimeout(() => (previewSlow.value = true), PREVIEW_SLOW_MS);
}
watch([() => bridge.connected.value, () => bridge.unsupported.value, url], waitForPreview, { immediate: true });
onBeforeUnmount(() => clearTimeout(previewTimer));
function reloadPreview() {
  if (frame.value && url.value) frame.value.src = url.value;
  waitForPreview();
}

const renderJson = computed(() => JSON.stringify([designer.preview.value, designer.sectionLabels.value]));
const sendRender = debounce(() => {
  if (!versionView) bridge.send(render());
}, 120);
watch(renderJson, sendRender);

const inspector = ref(true);
function toggleInspector() {
  inspector.value = !inspector.value;
  designer.hovered.value = null;
  if (inspector.value && !bridge.connected.value && frame.value && url.value) frame.value.src = url.value;
  bridge.send({ type: "inspector", enabled: !versionView && inspector.value });
}

watch(colorScheme, (scheme) => bridge.send({ type: "colorScheme", scheme }));

// Inline and !important on the blade's element: the shells pin a maximized blade with !important rules, and VcBlade passes no style through.
const designerRoot = ref<HTMLElement>();
watchEffect(() => {
  const blade = designerRoot.value?.closest<HTMLElement>(".vc-blade");
  for (const [property, value] of Object.entries(FULL_VIEW_STYLE)) {
    if (fullViewOn.value) blade?.style.setProperty(property, value, "important");
    else blade?.style.removeProperty(property);
  }
});

// In the capture phase, to run first: the shell closes the blade on Esc, which here only deselects.
useEventListener(
  window,
  "keydown",
  (event: KeyboardEvent) => {
    const target = event.target as HTMLElement | null;
    const root = designerRoot.value;
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

let selectedInPreview = false;
watch(
  () => {
    const sel = designer.selection.value;
    if (!sel || sel.kind === "page") return undefined;
    return sel.kind === "block" ? `${sel.sectionId}/${sel.id}` : sel.id;
  },
  (key) => {
    const fromPreview = selectedInPreview;
    selectedInPreview = false;
    if (!key) return sendRender();
    sendRender.cancel();
    if (!versionView) bridge.send(render({ scroll: !fromPreview }));
  },
);

function previewSection(type: string | null) {
  if (versionView) return;
  sendRender.cancel();
  if (type) bridge.send(render({ model: newItem(type, "section", designer.schemas.value) }));
  else sendRender();
}

const picking = ref(false);
const pickTypes = ref<string[]>();
let resolvePick: ((value: string | undefined) => void) | undefined;
function picked(asset: string | undefined) {
  resolvePick?.(asset);
  resolvePick = undefined;
  picking.value = false;
}
provide(
  PageAnchorsKey,
  computed(() =>
    pageAnchors(
      designer.doc.value.content,
      (item, kind) => (kind === "block" ? designer.blockSchema(item.type) : designer.sectionSchema(item.type)),
      designer.languages.value.default,
    ),
  ),
);
let menusOf: { storeId: string; lists: Promise<unknown[]> } | undefined;
provide(StoreMenusKey, () => {
  const storeId = designer.storeId.value;
  if (!storeId) return Promise.resolve([]);
  if (menusOf?.storeId !== storeId) menusOf = { storeId, lists: storeMenus(storeId) };
  const lists = menusOf.lists;
  // A failed read must be tried again next time.
  lists.catch(() => (menusOf = undefined));
  return lists;
});
provide(
  PaletteTokensKey,
  computed(() => ({ tokens: designer.handshake.value?.paletteTokens.light ?? [], colors: look.look.value.light })),
);
const whiteLabeled = computed(() => reported.value?.overriddenByWhiteLabeling ?? []);
provide(WhiteLabelingKey, whiteLabeled);
provide(PickAssetKey, (acceptedTypes) => {
  resolvePick?.(undefined);
  pickTypes.value = acceptedTypes;
  picking.value = true;
  return new Promise((resolve) => (resolvePick = resolve));
});

const save = async () => {
  try {
    await saveChanges();
  } finally {
    await Promise.all([designer.refreshThemeStatus(), reloadHistory()]);
  }
};

const reloadHistory = () => versionHistory.value?.reload().catch(() => undefined);

// The toast is rendered by the shell, outside this blade's scoped styles.
const ERROR_LIST_STYLE = { margin: "4px 0 0", paddingLeft: "18px", listStyle: "disc" };

function notifyFailure(title: string, e: unknown) {
  const pending = e instanceof CatalogPendingError ? [t("PAGE_BUILDER.DESIGNER.CATALOG_PENDING")] : [];
  const errors = e instanceof ApiError ? themeErrors(e.status, e.body) : pending;
  if (!errors.length) notification.error(title);
  else notification.error(h("div", [h("p", title), h("ul", { style: ERROR_LIST_STYLE }, errors.map((x) => h("li", x)))]), { timeout: false });
}

async function saveChanges() {
  const parts = [rules.pending(), ...look.pending()].filter((x) => !!x);
  if (!designer.dirty.value && !parts.length) return;
  const pageChanged = designer.dirty.value;
  const changed: HistoryFile[] = [...designer.changedParts.value, ...parts.map((x) => x.part)];
  try {
    const versions = await designer.save(parts.map((x) => x.file));
    if (!versions) return;
    for (const part of parts) part.saved(versions[part.file.path]);
    if (pageChanged) notifyPagesChanged();
  } catch (e) {
    if (e instanceof ApiError && e.status === 403) return notifyForbidden(changed);
    const messages: Record<number, string> = { 412: "PAGE_BUILDER.DESIGNER.CONFLICT", 428: "PAGE_BUILDER.DESIGNER.NO_VERSION" };
    notifyFailure(t(messages[e instanceof ApiError ? e.status : 0] ?? "PAGE_BUILDER.DESIGNER.SAVE_FAILED"), e);
  }
}

function notifyForbidden(changed: HistoryFile[]) {
  const template = designer.themeFileOpen.value;
  const refused = refusedParts(changed, template, hasAccess);
  if (!refused.length) notification.error(t("PAGE_BUILDER.DESIGNER.SAVE_FORBIDDEN_ANY"));
  else notification.error(t("PAGE_BUILDER.DESIGNER.SAVE_FORBIDDEN", { parts: refused.map((part) => t(partLabel(part, template))).join(", ") }));
}

async function publishTheme() {
  try {
    const publication = await designer.publishTheme();
    if (publication !== undefined) notification.success(t("PAGE_BUILDER.DESIGNER.THEME_PUBLISHED", { publication }));
  } catch (e) {
    notifyFailure(t("PAGE_BUILDER.DESIGNER.PUBLISH_THEME_FAILED"), e);
  }
  await reloadHistory();
}

async function publish(live: boolean, rebase = false) {
  try {
    await designer.publish(live, rebase);
    notifyPagesChanged();
    await reloadHistory();
  } catch (e) {
    // Git: a 409 means someone published another version meanwhile, and publishing over it is the editor's call.
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
    await reloadHistory();
  } catch {
    notification.error(t("PAGE_BUILDER.DESIGNER.PROMOTE_FAILED"));
  }
}

function openPreview() {
  const file = designer.file.value;
  if (designer.templateKey.value) {
    const url = previewUrlOf.value ? storefrontPage(storefront.value?.url, previewUrlOf.value) : undefined;
    if (url) window.open(url, "_blank", "noopener");
  } else if (file) {
    const href = filePreviewUrl(file, designer.gitFlow.value, storefront.value, !!designer.status.value.hasChanges);
    if (href) window.open(href, "_blank", "noopener");
  } else if (storefront.value && designer.groupId.value) {
    const url = storefrontPage(storefront.value.url, storefront.value.previewPath);
    url?.searchParams.set("pageId", designer.groupId.value);
    if (url) window.open(url, "_blank", "noopener");
  }
}

const sharedPanel = ref<InstanceType<typeof SharedPanel>>();
const openSharedComponentDesigner = useSharedComponentDesigner();

function insertShared(component: SharedComponent) {
  void designer.insertShared(component);
  mode.value = "sections";
}

function editShared(componentId: string) {
  const storeId = designer.storeId.value;
  if (!storeId) return;
  openSharedComponentDesigner(storeId, componentId, async () => {
    await designer.loadComponents(true);
    sendRender();
    sharedPanel.value?.reload();
  });
}

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
  if (!(await showConfirmation(t("PAGE_BUILDER.DESIGNER.SELECT_MODE.CONFIRM_DELETE", { count })))) return;
  const problem = designer.removeChecked();
  if (problem) notification.warning(t(problem));
}

const sharedName = ref<string>();
const savingShared = ref(false);
async function saveShared() {
  const name = sharedName.value?.trim();
  if (!name || savingShared.value) return;
  savingShared.value = true;
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
  } finally {
    savingShared.value = false;
  }
}

const historyOpen = ref(false);
const siteHistory = ref<HistoryStatus>();
const historyOffered = computed(() =>
  historyOffer({
    shared: !!designer.shared.value,
    filePage: !!designer.file.value && !designer.templateKey.value,
    gitFlow: designer.gitFlow.value,
    site: !!designer.groupId.value || !!designer.templateKey.value,
    status: siteHistory.value,
  }),
);
const previewing = ref<HistoryVersion>();
const versionHistory = ref<{ reload: () => Promise<void> }>();

const shownItems = computed(() =>
  historyItems({
    groupId: designer.groupId.value,
    templatePath: designer.templateKey.value ? designer.file.value?.path : undefined,
    theme: themeName.value,
    parts: [
      ...(rulesActive.value ? (["rules"] as const) : []),
      ...(designer.groupsPreview.value ? (["header", "footer"] as const) : []),
      ...(look.loaded.value ? Object.values(LOOK_FILES) : []),
    ],
  }),
);
const itemOf = (file: HistoryFile) => shownItems.value.find((x) => x.file === file)?.item;

const filesOf = (version: HistoryVersion) =>
  shownItems.value.filter((x) => version.items?.includes(x.item)).map((x) => x.file);
const describeVersion = (version: HistoryVersion) =>
  version.items &&
  filesOf(version)
    .map((file) => t(partLabel(file, !!designer.templateKey.value)))
    .join(", ");
const previewableVersion = (version: HistoryVersion) => !version.items || filesOf(version).some((x) => !THEME_ONLY_FILES.includes(x));
function restoreRefused(version: HistoryVersion) {
  const template = designer.themeFileOpen.value;
  const refused = refusedParts(filesOf(version), template, hasAccess);
  return refused.length ? t("PAGE_BUILDER.DESIGNER.HISTORY.RESTORE_FORBIDDEN", { parts: refused.map((part) => t(partLabel(part, template))).join(", ") }) : undefined;
}

async function historySource(cursor?: string) {
  const file = designer.file.value;
  if (file && !designer.templateKey.value) {
    const page = await fileHistory(file, cursor);
    return { versions: page.versions.map(fromGitVersion), hasMore: page.truncated, endCursor: page.endCursor };
  }
  const page = await contentHistory(designer.storeId.value!, shownItems.value.map((x) => x.item), cursor);
  siteHistory.value = page.status;
  return { versions: page.versions, hasMore: !!page.cursor, endCursor: page.cursor };
}

function openHistory() {
  designer.selection.value = null;
  historyOpen.value = true;
}

function closeHistory() {
  historyOpen.value = false;
}

watch(
  () => designer.selection.value,
  (selection) => {
    if (!selection) return;
    historyOpen.value = false;
    rulesOpen.value = false;
    if (previewing.value) previewVersion(null);
    else endVersionPreview();
  },
);

let versionPreviews = 0;
function endVersionPreview() {
  versionPreviews++;
  previewing.value = undefined;
  versionView = undefined;
}

async function previewVersion(version: HistoryVersion | null) {
  // A version read after another pick, or after Back, is not shown.
  const request = ++versionPreviews;
  const file = designer.file.value;
  if (version && file && !version.items) {
    window.open(filePreviewUrl(file, true, undefined, false, version.id), "_blank", "noopener");
    return;
  }
  sendRender.cancel();
  if (!version) {
    endVersionPreview();
    bridge.send(draft());
    bridge.send(render({ scroll: true }));
    bridge.send({ type: "inspector", enabled: inspector.value });
    return;
  }
  try {
    const storeId = designer.storeId.value!;
    const at = (file: HistoryFile) => {
      const item = itemOf(file);
      return item ? contentVersion(storeId, version.id, item) : Promise.resolve(undefined);
    };
    const groupAt = async (group: SectionGroup) => {
      const saved = (await at(group)) as { content?: unknown } | undefined;
      if (saved === undefined) return undefined;
      return Array.isArray(saved?.content) ? (saved.content as Item[]) : [];
    };
    const [saved, header, footer] = await Promise.all([
      at("page").then((x) => (x === undefined ? undefined : toDocument(x))),
      groupAt("header"),
      groupAt("footer"),
    ]);
    if (request !== versionPreviews) return;
    previewing.value = version;
    const groups = designer.groupsPreview.value;
    const shared = new Map([...designer.components].map(([id, x]) => [id, x && { content: x.content, label: x.name }]));
    // The version goes first: it ends any text being edited in the preview, and only then the inspector goes off.
    versionView = {
      draft: draft(groups && { header: header ?? groups.header, footer: footer ?? groups.footer }),
      render: render({ preview: saved ? resolveRefs(pageOnly(saved), shared) : undefined, selection: null }),
    };
    bridge.send(versionView.draft);
    bridge.send(versionView.render);
    bridge.send({ type: "inspector", enabled: false });
  } catch {
    if (request === versionPreviews) notification.error(t("PAGE_BUILDER.DESIGNER.HISTORY.PREVIEW_FAILED"));
  }
}
watch(
  () => designer.preview.value,
  () => {
    const shown = !!previewing.value;
    endVersionPreview();
    if (!shown) return;
    bridge.send(draft());
    sendRender.cancel();
    bridge.send(render());
    bridge.send({ type: "inspector", enabled: inspector.value });
  },
);

async function reopen() {
  const file = designer.file.value;
  await (file ? designer.open(file) : designer.open({ groupId: designer.groupId.value!, storeId: designer.storeId.value! }));
  endVersionPreview();
  await versionHistory.value?.reload();
}

async function restoreVersion(version: HistoryVersion) {
  if (!(await showConfirmation(t("PAGE_BUILDER.DESIGNER.HISTORY.CONFIRM", { version: version.label })))) return;
  if (designer.busy.value) return;
  try {
    if (version.items) await restoreSiteVersion(version, version.items);
    else {
      await designer.restore(version.id);
      await reloadHistory();
    }
    notification.success(t("PAGE_BUILDER.DESIGNER.HISTORY.RESTORED"));
  } catch {
    notification.error(t("PAGE_BUILDER.DESIGNER.HISTORY.RESTORE_FAILED"));
  }
}

async function restoreSiteVersion(version: HistoryVersion, items: string[]) {
  const storeId = designer.storeId.value!;
  const files = filesOf(version);
  designer.busy.value = "restoring";
  try {
    await restoreContentVersion(storeId, version.id, items);
    if (files.includes("page")) notifyPagesChanged();
    await Promise.all([
      files.includes("rules") ? rules.load(storeId, themeName.value!).catch(() => undefined) : undefined,
      files.some((x) => Object.values<HistoryFile>(LOOK_FILES).includes(x)) ? loadTheme(storeId).catch(() => undefined) : undefined,
    ]);
    if (files.some((x) => !THEME_ONLY_FILES.includes(x))) await reopen().catch(() => undefined);
    else await Promise.all([versionHistory.value?.reload(), designer.refreshThemeStatus()]).catch(() => undefined);
  } finally {
    designer.busy.value = undefined;
  }
}

// A draft never published is the only copy, so only drafts with a published version can be thrown away.
const discardChoices = computed(() =>
  discardOptions({
    page: designer.discardable.value.page,
    theme: designer.discardable.value.theme,
    template: designer.themeFileOpen.value,
    hasAccess,
  }),
);
const hasDraft = computed(() => discardChoices.value.some((x) => x.allowed));
const discarding = ref<DiscardOption[]>();

async function loadTheme(storeId: string) {
  await look.load(storeId, themeName.value!);
}

async function discardChanges() {
  if (hasDraft.value) discarding.value = discardChoices.value;
  else if (await showConfirmation(t("PAGE_BUILDER.DESIGNER.CONFIRM_DISCARD_UNSAVED"))) await discardDrafts({ page: false, theme: [] });
}

async function discardDrafts(choice: { page: boolean; theme: ThemePart[] }) {
  discarding.value = undefined;
  const drafts = choice.page || choice.theme.length > 0;
  let failed = false;
  try {
    if (drafts) await designer.discardDrafts(choice);
    if (choice.page) notifyPagesChanged();
  } catch {
    failed = true;
  }
  const storeId = designer.storeId.value;
  await Promise.all([
    storeId && themeName.value ? loadTheme(storeId).catch(() => undefined) : look.revert(),
    rules.loaded.value ? rules.reload().catch(() => undefined) : undefined,
  ]);
  await reopen();
  if (failed) notification.error(t("PAGE_BUILDER.DESIGNER.HISTORY.DISCARD_FAILED"));
  else if (drafts) notification.success(t("PAGE_BUILDER.DESIGNER.HISTORY.DISCARDED"));
}

// A template has no Publish of its own: it goes live with the theme.
const publishShown = computed(() =>
  publishActions({ template: designer.themeFileOpen.value, shared: !!designer.shared.value, mayPublish: hasAccess(PERMISSIONS.publish) }),
);
const themePublish = computed(() =>
  themePublishButton({ status: designer.themeStatus.value, unsaved: unsaved.value, busy: !!designer.busy.value }),
);

const publishTitle = computed(() => {
  const status = designer.status.value;
  if (status.pending && !status.awaitingMerge) return "PAGE_BUILDER.DESIGNER.PUBLISHING";
  return status.awaitingMerge && status.hasChanges ? "PAGE_BUILDER.DESIGNER.RETRY_PUBLISH" : "PAGE_BUILDER.DESIGNER.PUBLISH";
});

const sitePanel = ref(false);
async function loadSiteHistory(storeId: string) {
  siteHistory.value = await historyStatus(storeId).catch(() => undefined);
}
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
      const [{ on }] = await Promise.all([gitFlowOf(storeId, file?.theme ?? (await storeTheme(storeId))), loadSiteHistory(storeId)]);
      // What is open may have changed to another page or a template, which stays as it is.
      if (!on || !file || file.type === "themes" || designer.file.value?.path !== file.path) return;
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
    isVisible: publishShown.value.page,
    disabled: !designer.canPublish.value,
    clickHandler: () => publish(true),
  },
  {
    id: "publish-theme",
    title: t(themePublish.value.label.key, { count: themePublish.value.label.count }),
    icon: "lucide-palette",
    isVisible: publishShown.value.theme,
    disabled: themePublish.value.disabled,
    clickHandler: publishTheme,
  },
  {
    id: "unpublish",
    title: t("PAGE_BUILDER.DESIGNER.UNPUBLISH"),
    icon: "lucide-cloud-off",
    isVisible: publishShown.value.page && (!!designer.status.value.published || !!designer.status.value.liveBehind),
    disabled: !designer.canUnpublish.value,
    clickHandler: () => publish(false),
  },
  {
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
    isVisible: !!designer.status.value.production && hasAccess(PERMISSIONS.promote),
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
    id: "rules",
    title: t("PAGE_BUILDER.DESIGNER.RULES.TITLE"),
    icon: "lucide-split",
    isVisible: !!designer.templateKey.value,
    clickHandler: () => (rulesOpen.value ? (rulesOpen.value = false) : openRules()),
  },
  {
    id: "history",
    title: t("PAGE_BUILDER.DESIGNER.HISTORY.TITLE"),
    icon: "lucide-history",
    isVisible: historyOffered.value === "history",
    clickHandler: () => (historyOpen.value ? closeHistory() : openHistory()),
  },
  {
    id: "enable-history",
    title: t("PAGE_BUILDER.GIT.TURN_ON"),
    icon: "lucide-history",
    isVisible: historyOffered.value === "enable",
    clickHandler: enableHistory,
  },
  {
    id: "full-view",
    title: t(fullView.value ? "PAGE_BUILDER.DESIGNER.EXIT_FULL_VIEW" : "PAGE_BUILDER.DESIGNER.FULL_VIEW"),
    icon: fullView.value ? "lucide-minimize-2" : "lucide-maximize-2",
    isVisible: designerShown.value,
    clickHandler: () => (fullView.value = !fullView.value),
  },
]);

const barActions = computed(() =>
  toolbar.value
    .filter((item) => item.isVisible !== false && (item.id !== "full-view" || !fullView.value))
    .map((item) => {
      const named = item.id === "publish" || item.id === "publish-theme" || item.id === "save";
      let variant: "primary" | "secondary" | "ghost" = "ghost";
      if (item.id === "save") variant = "primary";
      else if (item.id === "publish" || item.id === "publish-theme") variant = "secondary";
      return { ...item, variant, label: named ? item.title : undefined };
    }),
);

useBeforeUnload(unsaved);
// onBeforeClose returning true keeps the blade open.
onBeforeClose(async () => unsaved.value && !(await showConfirmation(t("PAGE_BUILDER.DESIGNER.DISCARD"))));

const { getApiClient } = useApiClient(PageBuilderPageClient);
onMounted(async () => {
  let storeId: string;
  let open: Promise<void>;
  const target = param.value ? undefined : (query.value ?? restoreQueryFromAddress());
  try {
    if (param.value) {
      group.value = await (await getApiClient()).getGroup(param.value);
      if (archived.value) return;
      storeId = group.value.storeId!;
      home = { groupId: param.value, storeId };
      open = designer.open(home);
    } else if (target?.storeId && target.sharedComponentId) {
      storeId = target.storeId;
      open = designer.open({ sharedComponentId: target.sharedComponentId, storeId });
    } else if (target?.storeId && target.path) {
      storeId = target.storeId;
      const page: FilePage = { storeId, path: target.path, type: target.type || "pages", theme: await storeTheme(storeId) };
      if (page.type !== "themes") home = page;
      open = designer.open(page);
    } else {
      openFailed.value = true;
      return;
    }
  } catch {
    openFailed.value = true;
    return;
  }
  // The assets library picker works on this store.
  useUrlParams().setStoreId(storeId);
  void previewAs.load(storeId);
  await Promise.all([
    open,
    storeTheme(storeId).then((name) => {
      themeName.value = name;
      return Promise.all([
        loadTheme(storeId),
        target?.sharedComponentId ? undefined : loadSiteHistory(storeId),
        target?.sharedComponentId ? undefined : loadTemplateKeys(),
        // The preview gets the rules with the theme: they choose a content page's template too.
        target?.sharedComponentId || rules.loaded.value ? undefined : rules.load(storeId, name).catch(() => undefined),
      ]);
    }).catch(() => undefined),
    loadStorefront(storeId)
      .then((x) => {
        storefront.value = x;
        catalog.value = x.catalog;
        storeLanguages.value = x.languages;
        url.value = previewUrl(x.url, x.previewPath);
      })
      .catch(() => undefined)
      .finally(() => (storefrontChecked.value = true)),
  ]);
  homeTitle.value = home && !designer.templateKey.value ? title.value : undefined;
  // A shared component has no template: the address may hold the one of the designer it was opened from.
  const reloaded = target?.sharedComponentId ? undefined : templateInUrl();
  if (reloaded && reloaded !== designer.templateKey.value && pageTypeOf(reloaded) && switcherShown.value) await switchTo(reloaded);
  else if (designer.templateKey.value) await enterTemplate();
});

const bladeStack = inject(BladeStackKey, null);
const router = useRouter();
/** Read after the designer has loaded: on a reload the shell opens this blade without its query, while the address still reads "#/". */
function templateInUrl(): string | undefined {
  const value = query.value?.template ?? router.currentRoute.value.query.template;
  return typeof value === "string" ? value : undefined;
}
/**
 * A shared component or a file page is opened by query alone, and the shell reopens a blade from the address with its param only.
 * The history's location, not the current route: the shell opens this blade while the navigation to it is still under way.
 */
function restoreQueryFromAddress(): Record<string, string> | undefined {
  const address = router.resolve(router.options.history.location).query;
  const keys = address.sharedComponentId ? ["storeId", "sharedComponentId"] : ["storeId", "path", "type", "cultureName"];
  const restored: Record<string, string> = {};
  for (const key of keys) {
    const value = address[key];
    if (typeof value === "string" && value) restored[key] = value;
  }
  if (!restored.storeId || (!restored.sharedComponentId && !restored.path)) return undefined;
  // The stack writes the address from the blades' queries, so the next reload finds it there too.
  bladeStack?.updateBladeQuery(bladeId.value, restored);
  return restored;
}
watch(designer.templateKey, (key) => {
  bladeStack?.updateBladeQuery(bladeId.value, { template: key ?? "" });
  // The stack only keeps the query for its next URL write, so the URL is written here.
  const urlQuery = { ...router.currentRoute.value.query };
  if (key) urlQuery.template = key;
  else delete urlQuery.template;
  void router.replace({ query: urlQuery });
});

let home: { groupId: string; storeId: string } | FilePage | undefined;
const themeName = ref<string>();
const templateKeyList = ref<string[]>([]);
const readSavedTemplates = savedTemplatesReader();
let templatesRead = 0;
watch([() => designer.themeStatus.value, themeName], async ([status, theme]) => {
  const request = ++templatesRead;
  const storeId = designer.storeId.value;
  if (!status || !theme || !storeId) {
    savedTemplates.value = undefined;
    return;
  }
  try {
    const templates = await readSavedTemplates(`${storeId}/${theme}`, status, {
      keys: () => templateKeys(storeId, theme),
      read: async (key) => (await findTemplate({ storeId, theme, type: "themes", path: templatePath(theme, key) })).doc,
    });
    if (request === templatesRead) savedTemplates.value = templates;
  } catch {
    // A failed read keeps the templates read before.
  }
});
const catalog = ref<CatalogContext>();
watchEffect(() => {
  designer.language.value = editLanguage.value;
  designer.languages.value = { all: storeLanguages.value, default: catalog.value?.cultureName };
});
watch(editLanguage, () => {
  sendRender.cancel();
  if (!versionView) bridge.send(render());
});
const rulesOpen = ref(false);
const switcherShown = computed(() => !designer.shared.value && !!themeName.value && mayEditTemplates);
const reported = ref<Resolved>();
const rulesChoice = computed<RulesPick | undefined>(() => reported.value && rulesPick(reported.value, rules.loaded.value ? rules.rules.value : undefined));
const whyOf = (r: RulesPick) =>
  r.rule === null
    ? t("PAGE_BUILDER.DESIGNER.RULES.TEST_DEFAULT")
    : t("PAGE_BUILDER.DESIGNER.RULES.TEST_RULE", { number: r.rule === undefined ? (r.ruleId ?? "") : r.rule + 1 });

async function loadTemplateKeys() {
  const storeId = designer.storeId.value;
  if (storeId && themeName.value) templateKeyList.value = await templateKeys(storeId, themeName.value).catch(() => []);
}

function openRules() {
  designer.selection.value = null;
  historyOpen.value = false;
  rulesOpen.value = true;
}

const previewPicks = useLocalStorage<Record<string, NamedEntity>>("vc-page-designer-preview-with", {});
const pickKey = (type: PageType) => `${designer.storeId.value}:${type}`;
const previewed = computed(() => (designer.pageType.value ? previewPicks.value[pickKey(designer.pageType.value)] : undefined));
const previewUrlOf = computed(() => {
  const type = designer.pageType.value;
  if (!type) return undefined;
  return previewed.value ? entityUrl(type, previewed.value.id) : templateEntry(designer.schemas.value, type).entry.previewUrl;
});

function pickEntity(entity: NamedEntity) {
  const type = designer.pageType.value;
  if (!type || entity.id === previewed.value?.id) return;
  previewPicks.value = { ...previewPicks.value, [pickKey(type)]: entity };
}
watch(previewUrlOf, (to) => {
  if (!to || !designer.templateKey.value) return;
  reported.value = undefined;
  sendRender.cancel();
  if (previewing.value) void previewVersion(previewing.value);
  else bridge.send(render());
});

async function enterTemplate() {
  const type = designer.pageType.value;
  const ctx = catalog.value;
  const storeId = designer.storeId.value;
  if (!type || !ctx || !storeId) return;
  await Promise.all([
    rules.loaded.value || !themeName.value ? undefined : rules.load(storeId, themeName.value).catch(() => undefined),
    previewed.value
      ? undefined
      : (async () => {
          const first =
            type === "product"
              ? (await searchProducts(ctx))[0]
              : type === "category"
                ? (await searchCategories(ctx))[0]
                : (await searchPages(storeId)).map((x) => ({ id: x.permalink, name: x.name }))[0];
          if (first) previewPicks.value = { ...previewPicks.value, [pickKey(type)]: first };
        })().catch(() => undefined),
  ]);
}

async function switchTo(key: string | null) {
  const storeId = designer.storeId.value;
  if (!storeId || !themeName.value || (key === null && !home)) return;
  if (unsaved.value && !(await showConfirmation(t("PAGE_BUILDER.DESIGNER.TEMPLATES.CONFIRM_SWITCH")))) return;
  look.revert();
  if (rules.dirty.value) await rules.load(storeId, themeName.value).catch(() => undefined);
  historyOpen.value = false;
  endVersionPreview();
  reported.value = undefined;
  const opened = key ? await openTemplate({ storeId, theme: themeName.value, type: "themes", path: templatePath(themeName.value, key) }) : await openHome();
  if (opened === "template") await enterTemplate();
  else rulesOpen.value = false;
  reloadPreview();
}

async function openHome(): Promise<"page"> {
  await designer.open(home!);
  return "page";
}

async function openTemplate(source: FilePage): Promise<"template" | "page" | "failed"> {
  await designer.open(source);
  if (!designer.loadError.value) return "template";
  if (!home) return "failed";
  notification.error(t("PAGE_BUILDER.DESIGNER.TEMPLATES.OPEN_FAILED"));
  return openHome();
}

const creating = ref<{ type: PageType; name: string }>();
const newKey = computed(() => (creating.value ? alternateKey(creating.value.type, creating.value.name) : undefined));
const newKeyTaken = computed(() => !!newKey.value && templateKeyList.value.includes(newKey.value));
const startCreate = (type: PageType) => (creating.value = { type, name: "" });
const creatingNow = ref(false);
async function createTemplate() {
  if (creatingNow.value) return;
  creatingNow.value = true;
  try {
    await createAndOpenTemplate();
  } finally {
    creatingNow.value = false;
  }
}
async function createAndOpenTemplate() {
  const key = newKey.value;
  const storeId = designer.storeId.value;
  const target = creating.value;
  if (!key || !target || newKeyTaken.value || !storeId || !themeName.value) return;
  if (unsaved.value && !(await showConfirmation(t("PAGE_BUILDER.DESIGNER.TEMPLATES.CONFIRM_SWITCH")))) return;
  const file = (k: string): FilePage => ({ storeId, theme: themeName.value!, type: "themes", path: templatePath(themeName.value!, k) });
  try {
    const [base, created] = await Promise.all([findFilePage(file(target.type)), loadThemeFile(storeId, file(key).path)]);
    // Someone created it meanwhile: the name is taken.
    if (created.content !== undefined) {
      templateKeyList.value = [...new Set([...templateKeyList.value, key])].sort();
      return;
    }
    const doc = startingTemplate(target.type, base, templateEntry(designer.schemas.value, target.type).entry, designer.mainItem);
    const content = filePageContent({ ...doc, settings: { ...doc.settings, name: target.name.trim() } });
    await saveDesigner(storeId, { themeFiles: [{ path: file(key).path, eTag: created.eTag, content }] }, designer.handshake.value?.hash);
  } catch (e) {
    notifyFailure(t("PAGE_BUILDER.DESIGNER.TEMPLATES.CREATE_FAILED"), e);
    return;
  }
  creating.value = undefined;
  templateKeyList.value = [...new Set([...templateKeyList.value, key])].sort();
  // The new template opens as saved, so there is nothing unsaved to ask about.
  look.revert();
  if (rules.dirty.value) await rules.load(storeId, themeName.value).catch(() => undefined);
  if ((await openTemplate(file(key))) === "template") await enterTemplate();
  else rulesOpen.value = false;
  reloadPreview();
}

const inEffect = computed(() => {
  const key = designer.templateKey.value;
  const r = rulesChoice.value;
  if (!key || !r || r.pageType !== designer.pageType.value) return undefined;
  const why = whyOf(r);
  const other = r.template !== key;
  return {
    text: t("PAGE_BUILDER.DESIGNER.TEMPLATES.IN_EFFECT", { name: templateName(r.template), why }),
    other,
    title: other ? t("PAGE_BUILDER.DESIGNER.TEMPLATES.IN_EFFECT_OTHER", { name: previewed.value?.name ?? "", template: templateName(r.template) }) : undefined,
  };
});

// The storefront's descriptors lack these page-level fields; a page's Template is stored as settings.template.
watchEffect(() => {
  const type = designer.pageType.value;
  const page = !designer.shared.value && !type && (!!designer.groupId.value || designer.file.value?.type === "pages");
  const fields: Field[] = [];
  if (type === "page") {
    fields.push({
      id: "layout",
      type: "select",
      label: t("PAGE_BUILDER.DESIGNER.TEMPLATES.LAYOUT"),
      required: true,
      default: "full",
      options: ["full", "minimal", "blank"].map((value) => ({ value, label: t(`PAGE_BUILDER.DESIGNER.TEMPLATES.LAYOUTS.${value.toUpperCase()}`) })),
    });
  } else if (page) {
    const chosen = designer.doc.value.settings.template;
    const r = rulesChoice.value?.pageType === "page" ? rulesChoice.value : undefined;
    let info = t("PAGE_BUILDER.DESIGNER.TEMPLATES.TEMPLATE_HINT");
    if (typeof chosen === "string" && chosen) info = t("PAGE_BUILDER.DESIGNER.TEMPLATES.TEMPLATE_CHOSEN", { name: templateName(chosen) });
    else if (r)
      info = t("PAGE_BUILDER.DESIGNER.TEMPLATES.TEMPLATE_IN_EFFECT", { name: templateName(r.template), why: whyOf(r) });
    fields.push({
      id: "template",
      type: "select",
      label: t("PAGE_BUILDER.DESIGNER.TEMPLATES.TEMPLATE"),
      placeholder: t("PAGE_BUILDER.DESIGNER.TEMPLATES.BY_RULES"),
      info,
      options: ["page", ...templateKeyList.value.filter((key) => key !== "page" && pageTypeOf(key) === "page")].map((value) => ({ value, label: templateName(value) })),
    });
  }
  designer.extraPageFields.value = fields;
});
</script>

<style lang="scss" scoped>
@media (min-width: 768px) {
  .pd-full-view :deep(.vc-blade__header),
  .pd-full-view :deep(.vc-blade__toolbar),
  .pd-own-actions :deep(.vc-blade__toolbar) {
    display: none;
  }

  // The admin shell's environment ribbon holds the top-right corner, so the bar keeps clear of it.
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
    // A squeezed bar spills its controls under the preview, so it keeps the height of its wrapped lines.
    flex: none;
    display: flex;
    flex-wrap: wrap;
    align-content: flex-start;
    align-items: center;
    gap: 8px;
    padding: 8px 16px;

    :deep(.vc-button) {
      flex: none;
      white-space: nowrap;
    }

    // The shell's dropdown is 100% high, which runs away in a content-sized bar once it wraps.
    :deep(.vc-dropdown) {
      height: auto;
    }
  }

  &__spacer {
    flex: 1;
  }

  &__status {
    flex: none;
    padding: 2px 10px;
    white-space: nowrap;
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
    &--behind,
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

  &__schemes {
    margin-right: 8px;
  }

  &__picker {
    align-items: center;
    gap: 6px;
    font: inherit;
    font-size: 12px;
    white-space: nowrap;
  }

  &__picker-name {
    max-width: 10rem;
    overflow: hidden;
    text-overflow: ellipsis;
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

  &--fit {
    width: auto;
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
