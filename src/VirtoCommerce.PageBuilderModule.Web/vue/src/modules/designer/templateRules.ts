import { computed, ref } from "vue";
import * as api from "./api";
import { rulesPath } from "./history";
import { formatRules, readRules, type TemplateRules } from "./rules";

/**
 * The store's template rules as the designer edits them: the theme's rules file as last saved (its draft, else what is
 * live; a page type without a list has its default rules). Save writes a draft, always as version 2; the theme's
 * publication puts it live with the rest of the theme.
 */
export function useTemplateRules() {
  const rules = ref<TemplateRules>(readRules(undefined));
  const savedJson = ref(formatRules(rules.value));
  const loaded = ref(false);
  let at: { storeId: string; theme: string } | undefined;
  const dirty = computed(() => loaded.value && formatRules(rules.value) !== savedJson.value);

  async function load(storeId: string, theme: string) {
    at = { storeId, theme };
    rules.value = readRules(await api.loadThemeFile(storeId, theme, rulesPath(theme)));
    savedJson.value = formatRules(rules.value);
    loaded.value = true;
  }

  /** What Save writes: the rules file, always as version 2, and how to mark it saved once it is */
  function pending(): { file: { path: string; content: unknown }; saved: () => void } | undefined {
    if (!at || !dirty.value) return undefined;
    const sent = formatRules(rules.value);
    return { file: { path: rulesPath(at.theme), content: JSON.parse(sent) }, saved: () => (savedJson.value = sent) };
  }

  /** Throws the unsaved edits away and, with `draft`, the saved draft (a never-published file keeps it: it is the only copy) */
  async function discard(draft: boolean) {
    if (!at) return;
    if (draft) await api.discardThemeDraft(at.storeId, at.theme, rulesPath(at.theme));
    await load(at.storeId, at.theme);
  }

  return { rules, dirty, loaded, load, pending, discard };
}

export type TemplateRulesState = ReturnType<typeof useTemplateRules>;
