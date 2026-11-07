import { computed, ref } from "vue";
import * as api from "./api";
import type { DesignerSaveFile } from "./designerSave";
import type { ColorScheme } from "./previewMessages";
import { brandPath, lookDraft, lookFiles, palettePath, readBrand, readColors, type BrandField, type ThemeLook } from "./themeLook";

const emptyLook = (): ThemeLook => ({ brand: {}, light: {}, dark: {} });
const copy = (look: ThemeLook): ThemeLook => JSON.parse(JSON.stringify(look)) as ThemeLook;

/** The theme's brand and palettes being edited: what applies in the preview and what Save writes as drafts */
export function useThemeLook() {
  let at: { storeId: string; theme: string } | undefined;
  const look = ref<ThemeLook>(emptyLook());
  const saved = ref<ThemeLook>(emptyLook());
  const loaded = ref(false);
  const loadError = ref<unknown>();
  const dirty = computed(() => loaded.value && !!at && lookFiles(at.theme, look.value, saved.value).length > 0);
  const draft = computed(() => (loaded.value ? lookDraft(look.value) : undefined));

  /** The brand file and the palettes as last saved; `settings` are the theme settings, the brand of a theme without its file */
  async function load(storeId: string, theme: string, settings: Record<string, unknown>) {
    at = { storeId, theme };
    loaded.value = false;
    loadError.value = undefined;
    try {
      const [brand, light, dark] = await Promise.all(
        [brandPath(theme), palettePath(theme, "light"), palettePath(theme, "dark")].map((path) => api.loadThemeFile(storeId, theme, path)),
      );
      saved.value = { brand: readBrand(brand, settings), light: readColors(light), dark: readColors(dark) };
      look.value = copy(saved.value);
      loaded.value = true;
    } catch (e) {
      loadError.value = e;
    }
  }

  function setBrand(field: BrandField, url: string | null) {
    look.value = { ...look.value, brand: { ...look.value.brand, [field]: url || undefined } };
  }

  /** Sets a token's color; none: the storefront preset's color */
  function setColor(scheme: ColorScheme, token: string, color: string | null) {
    const colors = { ...look.value[scheme] };
    if (color) colors[token] = color;
    else delete colors[token];
    look.value = { ...look.value, [scheme]: colors };
  }

  function revert() {
    look.value = copy(saved.value);
  }

  /** What Save writes: a file per changed part, and how to mark it saved once it is */
  function pending(): { file: DesignerSaveFile; saved: () => void }[] {
    if (!at || !loaded.value) return [];
    const sent = copy(look.value);
    return lookFiles(at.theme, sent, saved.value).map((file) => ({
      file,
      saved: () => {
        // what was sent, part by part: an edit made while it was on its way stays unsaved
        const part = file.path === brandPath(at!.theme) ? "brand" : file.path === palettePath(at!.theme, "dark") ? "dark" : "light";
        saved.value = { ...saved.value, [part]: sent[part] };
      },
    }));
  }

  return { look, loaded, loadError, dirty, draft, load, setBrand, setColor, revert, pending };
}

export type ThemeLookState = ReturnType<typeof useThemeLook>;
