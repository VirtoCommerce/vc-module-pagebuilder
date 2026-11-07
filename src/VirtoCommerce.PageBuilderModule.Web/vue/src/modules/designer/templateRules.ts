import { computed, ref } from "vue";
import * as api from "./api";
import { rulesPath } from "./history";
import { formatRules, readRules, unreadableRules, type TemplateRules } from "./rules";

/**
 * The store's template rules as the designer edits them: the theme's rules file as last saved (its draft, else what is
 * live; a page type without a list has its default rules). Save writes a draft, always as version 2; the theme's
 * publication puts it live with the rest of the theme.
 */
export function useTemplateRules() {
  const rules = ref<TemplateRules>(readRules(undefined));
  const savedJson = ref(formatRules(rules.value));
  const loaded = ref(false);
  /** the entries of the file as last read that the designer can't read: the next save of the rules leaves them out */
  const unreadable = ref(0);
  let at: { storeId: string; theme: string } | undefined;
  const dirty = computed(() => loaded.value && formatRules(rules.value) !== savedJson.value);

  async function load(storeId: string, theme: string) {
    at = { storeId, theme };
    const file = await api.loadThemeFile(storeId, theme, rulesPath(theme));
    rules.value = readRules(file);
    unreadable.value = unreadableRules(file);
    savedJson.value = formatRules(rules.value);
    loaded.value = true;
  }

  /** What Save writes: the rules file, always as version 2, and how to mark it saved once it is */
  function pending(): { part: "rules"; file: { path: string; content: unknown }; saved: () => void } | undefined {
    if (!at || !dirty.value) return undefined;
    const sent = formatRules(rules.value);
    return {
      part: "rules",
      file: { path: rulesPath(at.theme), content: JSON.parse(sent) },
      saved: () => {
        savedJson.value = sent;
        unreadable.value = 0;
      },
    };
  }

  /** Reads the rules again: the unsaved edits go */
  async function reload() {
    if (at) await load(at.storeId, at.theme);
  }

  return { rules, dirty, loaded, unreadable, load, pending, reload };
}

export type TemplateRulesState = ReturnType<typeof useTemplateRules>;
