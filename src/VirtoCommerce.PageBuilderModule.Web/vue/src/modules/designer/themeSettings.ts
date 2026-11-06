import { computed, ref } from "vue";
import type { Field } from "./types";
import { getAuthHeaders } from "../page-builder/composables/usePageContentApi";
import { clone, valuesOf } from "./schema";
import { saveUrl } from "./themePublication";
import { ApiError } from "./api";

/**
 * The theme's settings (logo, colours, …): `{theme}/config/settings_data.json` of the store's theme, as the Angular
 * designer edited it. `current` is a preset's name or the settings themselves; `presets` are named settings.
 */
export interface ThemeSettingsData {
  current?: string | Record<string, unknown>;
  presets?: Record<string, Record<string, unknown> | { settings?: Record<string, unknown> }>;
}

export interface ThemeSettingsGroup {
  name: string;
  icon?: string;
  settings: Field[];
}

/** The theme's settings file, as saves name it */
export const settingsDataPath = (theme: string) => `${theme}/config/settings_data.json`;

const presetValues = (preset: unknown): Record<string, unknown> => {
  const value = (preset ?? {}) as { settings?: Record<string, unknown> };
  return clone(value.settings ?? (preset as Record<string, unknown>) ?? {});
};

/** The settings a theme data file applies (a preset's when `current` names one) */
export function currentSettings(data: ThemeSettingsData): Record<string, unknown> {
  if (typeof data.current === "string") return presetValues(data.presets?.[data.current]);
  return clone(data.current ?? {});
}

/** A theme config file as last saved: its draft, else what is live */
async function template(storeId: string, theme: string, path: string): Promise<unknown> {
  const query = new URLSearchParams({ storeId, theme, type: "themes", path: `${theme}/config/${path}`, draft: "true" });
  const res = await fetch(`/api/pagebuilder/template?${query}`, { headers: await getAuthHeaders() });
  if (res.status === 404) return undefined;
  if (!res.ok) throw new Error(`${res.status}`);
  const text = await res.text();
  return text.trim() ? JSON.parse(text) : undefined;
}

/**
 * The theme settings being edited. `settings` is what the theme sets plus the edits: what applies (sent to the preview
 * as it changes) and what Save writes as the theme's current settings, a draft the theme's publication puts live. `values`
 * adds the schema's defaults for what the theme doesn't set, for the fields to show; a default isn't applied or saved,
 * as it needn't match the storefront's own look (its preset's colours, say).
 */
export function useThemeSettings() {
  const store = ref<{ storeId: string; theme: string }>();
  const data = ref<ThemeSettingsData>({});
  const settings = ref<Record<string, unknown>>({});
  const defaults = ref<Record<string, unknown>>({});
  const values = computed(() => ({ ...defaults.value, ...settings.value }));
  const serverGroups = ref<ThemeSettingsGroup[]>([]);
  const savedJson = ref("{}");
  const loaded = ref(false);
  /** why the theme's settings couldn't be read: nothing is saved then, as a save writes the whole file */
  const loadError = ref<unknown>();
  const dirty = computed(() => loaded.value && JSON.stringify(settings.value) !== savedJson.value);
  const presets = computed(() => Object.keys(data.value.presets ?? {}));

  async function load(storeId: string, theme: string) {
    store.value = { storeId, theme };
    loaded.value = false;
    loadError.value = undefined;
    try {
      // a theme without the file starts from no settings; a file that can't be read is not a theme without one
      const [settingsData, schema] = await Promise.all([
        template(storeId, theme, "settings_data.json"),
        template(storeId, theme, "settings_schema.json").catch(() => undefined),
      ]);
      data.value = (settingsData as ThemeSettingsData) ?? { current: {}, presets: {} };
      serverGroups.value = Array.isArray(schema) ? (schema as ThemeSettingsGroup[]) : [];
      settings.value = currentSettings(data.value);
      savedJson.value = JSON.stringify(settings.value);
      loaded.value = true;
    } catch (e) {
      loadError.value = e;
    }
  }

  /** The schema's defaults, shown for the settings the theme doesn't set */
  function withDefaults(groups: ThemeSettingsGroup[]) {
    defaults.value = Object.fromEntries(Object.entries(valuesOf(groups.flatMap((x) => x.settings))).filter(([, v]) => v !== undefined));
  }

  function set(id: string, value: unknown) {
    settings.value = { ...settings.value, [id]: value };
  }

  /** A preset's settings become the current ones (still to be saved) */
  function applyPreset(name: string) {
    settings.value = { ...settings.value, ...presetValues(data.value.presets?.[name]) };
  }

  function revert() {
    settings.value = JSON.parse(savedJson.value) as Record<string, unknown>;
  }

  /** Saves the settings as the theme's draft; with the catalog's hash, the server checks them as it checks every theme file */
  async function save(catalogHash?: string) {
    if (!store.value || !loaded.value) return;
    const { storeId, theme } = store.value;
    const current = clone(settings.value);
    const next: ThemeSettingsData = { ...data.value, current };
    const files = [{ path: settingsDataPath(theme), type: "themes", content: next }];
    const res = await fetch(saveUrl(storeId, theme, catalogHash), {
      method: "POST",
      headers: { ...(await getAuthHeaders()), "Content-Type": "application/json" },
      body: JSON.stringify({ files: JSON.stringify(files) }),
    });
    if (!res.ok) throw new ApiError(res.status, await res.text().catch(() => ""));
    data.value = next;
    // what was sent: an edit made while it was on its way stays unsaved
    savedJson.value = JSON.stringify(current);
  }

  return { settings, values, serverGroups, presets, dirty, loaded, loadError, load, withDefaults, set, applyPreset, revert, save };
}

export type ThemeSettings = ReturnType<typeof useThemeSettings>;
