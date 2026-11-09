import { computed, ref } from "vue";
import * as api from "./api";
import type { DesignerSaveFile } from "./designerSave";
import { THEME_FILES, type ThemePart } from "./history";
import { clone, isRecord } from "./json";
import type { ColorScheme } from "./previewMessages";
import { LOOK_FILES, brandFile, lookDraft, lookFiles, readBrand, readColors, settingsDataPath, type BrandField, type ThemeLook } from "./themeLook";
import { undoStep, withGenerated, withStep, type GeneratedPalettes, type PaletteStep } from "./paletteContrast";

const emptyLook = (): ThemeLook => ({ brand: {}, light: {}, dark: {} });

export function useThemeLook() {
  let at: { storeId: string; theme: string } | undefined;
  const look = ref<ThemeLook>(emptyLook());
  const saved = ref<ThemeLook>(emptyLook());
  const loaded = ref(false);
  const loadError = ref<unknown>();
  let eTags: Record<string, string> = {};
  const steps = ref<PaletteStep[]>([]);
  const canUndoColors = computed(() => steps.value.length > 0);
  // Typing a color edits one token many times, which is one step to undo.
  let lastEdited: string | undefined;
  const dirty = computed(() => loaded.value && !!at && lookFiles(at.theme, look.value, saved.value).length > 0);
  /** An empty brand in a brand file means no logos, so the published ones must not show. */
  const hasBrandFile = ref(false);
  const draft = computed(() =>
    loaded.value ? lookDraft(look.value, hasBrandFile.value || JSON.stringify(brandFile(look.value.brand)) !== JSON.stringify(brandFile(saved.value.brand))) : undefined,
  );

  async function load(storeId: string, theme: string) {
    at = { storeId, theme };
    loaded.value = false;
    loadError.value = undefined;
    try {
      const paths = [LOOK_FILES.brand, LOOK_FILES.light, LOOK_FILES.dark].map((part) => THEME_FILES[part](theme));
      const files = await Promise.all(paths.map((path) => api.loadThemeFile(storeId, path)));
      const [brand, light, dark] = files.map((x) => x.content);
      hasBrandFile.value = isRecord(brand);
      const settingsData = hasBrandFile.value ? undefined : (await api.loadThemeFile(storeId, settingsDataPath(theme))).content;
      eTags = Object.fromEntries(paths.map((path, i) => [path, files[i].eTag]));
      saved.value = { brand: readBrand(brand, settingsData), light: readColors(light), dark: readColors(dark) };
      look.value = clone(saved.value);
      steps.value = [];
      lastEdited = undefined;
      loaded.value = true;
    } catch (e) {
      loadError.value = e;
    }
  }

  function setBrand(field: BrandField, url: string | null) {
    look.value = { ...look.value, brand: { ...look.value.brand, [field]: url || undefined } };
  }

  function setColor(scheme: ColorScheme, token: string, color: string | null) {
    if ((look.value[scheme][token] ?? null) === (color || null)) return;
    if (lastEdited !== `${scheme}:${token}`) steps.value = withStep(steps.value, look.value);
    lastEdited = `${scheme}:${token}`;
    const colors = { ...look.value[scheme] };
    if (color) colors[token] = color;
    else delete colors[token];
    look.value = { ...look.value, [scheme]: colors };
  }

  function applyGenerated(generated: GeneratedPalettes) {
    const next = withGenerated(look.value, steps.value, generated);
    look.value = next.look;
    steps.value = next.steps;
    lastEdited = undefined;
  }

  function undoColors() {
    const previous = undoStep(look.value, steps.value);
    if (!previous) return;
    look.value = previous.look;
    steps.value = previous.steps;
    lastEdited = undefined;
  }

  function revert() {
    look.value = clone(saved.value);
    steps.value = [];
    lastEdited = undefined;
  }

  function pending(): { part: ThemePart; file: DesignerSaveFile; saved: (eTag?: string) => void }[] {
    if (!at || !loaded.value) return [];
    const sent = clone(look.value);
    return lookFiles(at.theme, sent, saved.value).map(({ part, file }) => ({
      part: LOOK_FILES[part],
      file: { ...file, eTag: eTags[file.path] ?? "" },
      // What was sent, part by part, so that an edit made while it was on its way stays unsaved.
      saved: (eTag?: string) => {
        saved.value = { ...saved.value, [part]: sent[part] };
        if (part === "brand") hasBrandFile.value = true;
        if (eTag) eTags[file.path] = eTag;
      },
    }));
  }

  return { look, loaded, loadError, dirty, draft, load, setBrand, setColor, applyGenerated, canUndoColors, undoColors, revert, pending };
}

export type ThemeLookState = ReturnType<typeof useThemeLook>;
