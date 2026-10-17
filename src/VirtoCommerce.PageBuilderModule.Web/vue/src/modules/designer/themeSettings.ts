import { computed, ref } from "vue";
import type { Field } from "./types";
import { getAuthHeaders } from "../page-builder/composables/usePageContentApi";
import { clone, valuesOf } from "./schema";

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

const presetValues = (preset: unknown): Record<string, unknown> => {
  const value = (preset ?? {}) as { settings?: Record<string, unknown> };
  return clone(value.settings ?? (preset as Record<string, unknown>) ?? {});
};

/** The settings a theme data file applies (a preset's when `current` names one) */
export function currentSettings(data: ThemeSettingsData): Record<string, unknown> {
  if (typeof data.current === "string") return presetValues(data.presets?.[data.current]);
  return clone(data.current ?? {});
}

async function template(storeId: string, theme: string, path: string): Promise<unknown> {
  const query = new URLSearchParams({ storeId, theme, type: "themes", path: `${theme}/config/${path}` });
  const res = await fetch(`/api/pagebuilder/template?${query}`, { headers: await getAuthHeaders() });
  if (res.status === 404) return undefined;
  if (!res.ok) throw new Error(`${res.status}`);
  const text = await res.text();
  return text.trim() ? JSON.parse(text) : undefined;
}

/**
 * The theme settings being edited. `settings` is what the theme sets plus the edits: what applies (sent to the preview
 * as it changes) and what Save writes as the theme's current settings (live: theme settings have no draft). `values`
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
  const dirty = computed(() => loaded.value && JSON.stringify(settings.value) !== savedJson.value);
  const presets = computed(() => Object.keys(data.value.presets ?? {}));

  async function load(storeId: string, theme: string) {
    store.value = { storeId, theme };
    const [settingsData, schema] = await Promise.all([
      template(storeId, theme, "settings_data.json").catch(() => undefined),
      template(storeId, theme, "settings_schema.json").catch(() => undefined),
    ]);
    data.value = (settingsData as ThemeSettingsData) ?? { current: {}, presets: {} };
    serverGroups.value = Array.isArray(schema) ? (schema as ThemeSettingsGroup[]) : [];
    settings.value = currentSettings(data.value);
    savedJson.value = JSON.stringify(settings.value);
    loaded.value = true;
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

  async function save() {
    if (!store.value) return;
    const { storeId, theme } = store.value;
    const next: ThemeSettingsData = { ...data.value, current: clone(settings.value) };
    const files = [{ path: `${theme}/config/settings_data.json`, type: "themes", content: next }];
    const res = await fetch(`/api/pagebuilder/save?${new URLSearchParams({ storeId, theme })}`, {
      method: "POST",
      headers: { ...(await getAuthHeaders()), "Content-Type": "application/json" },
      body: JSON.stringify({ files: JSON.stringify(files) }),
    });
    if (!res.ok) throw new Error(`${res.status} ${await res.text().catch(() => "")}`);
    data.value = next;
    savedJson.value = JSON.stringify(settings.value);
  }

  return { settings, values, serverGroups, presets, dirty, loaded, load, withDefaults, set, applyPreset, revert, save };
}

export type ThemeSettings = ReturnType<typeof useThemeSettings>;
