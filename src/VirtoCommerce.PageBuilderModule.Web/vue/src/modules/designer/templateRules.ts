import { computed, ref } from "vue";
import * as api from "./api";
import { rulesPath } from "./history";
import { formatRules, rulesFromFile, type TemplateRules } from "./rules";

/**
 * The store's template rules as the designer edits them: the theme's rules file as last saved (its draft, else what is
 * live; no file: the storefront's default). Save writes a draft; the theme's publication puts it live with the rest of
 * the theme.
 */
export function useTemplateRules() {
  const rules = ref<TemplateRules>(rulesFromFile(undefined));
  const savedJson = ref(formatRules(rules.value));
  const loaded = ref(false);
  let at: { storeId: string; theme: string } | undefined;
  const dirty = computed(() => loaded.value && formatRules(rules.value) !== savedJson.value);

  async function load(storeId: string, theme: string) {
    at = { storeId, theme };
    rules.value = rulesFromFile(await api.loadThemeFile(storeId, theme, rulesPath(theme)));
    savedJson.value = formatRules(rules.value);
    loaded.value = true;
  }

  /** Saves the rules as the theme's draft; with the catalog's hash, the server checks them as it checks every theme file */
  async function save(catalogHash?: string) {
    if (!at || !dirty.value) return;
    const sent = formatRules(rules.value);
    await api.saveThemeFiles(at.storeId, at.theme, [{ path: rulesPath(at.theme), content: JSON.parse(sent) }], catalogHash);
    savedJson.value = sent;
  }

  /** Throws the unsaved edits away and, with `draft`, the saved draft (a never-published file keeps it: it is the only copy) */
  async function discard(draft: boolean) {
    if (!at) return;
    if (draft) await api.discardThemeDraft(at.storeId, at.theme, rulesPath(at.theme));
    await load(at.storeId, at.theme);
  }

  return { rules, dirty, loaded, load, save, discard };
}

export type TemplateRulesState = ReturnType<typeof useTemplateRules>;
