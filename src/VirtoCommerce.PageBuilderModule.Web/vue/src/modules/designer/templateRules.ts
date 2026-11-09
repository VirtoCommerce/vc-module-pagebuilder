import { computed, ref } from "vue";
import * as api from "./api";
import type { DesignerSaveFile } from "./designerSave";
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
  /** the version of the file as last read or saved: Save sends it back */
  let eTag = "";
  const dirty = computed(() => loaded.value && formatRules(rules.value) !== savedJson.value);

  async function load(storeId: string, theme: string) {
    at = { storeId, theme };
    const file = await api.loadThemeFile(storeId, rulesPath(theme));
    eTag = file.eTag;
    rules.value = readRules(file.content);
    unreadable.value = unreadableRules(file.content);
    savedJson.value = formatRules(rules.value);
    loaded.value = true;
  }

  /** What Save writes: the rules file, always as version 2, with the version it was read at; and how to mark it saved at its new version */
  function pending(): { part: "rules"; file: DesignerSaveFile; saved: (eTag?: string) => void } | undefined {
    if (!at || !dirty.value) return undefined;
    const sent = formatRules(rules.value);
    return {
      part: "rules",
      file: { path: rulesPath(at.theme), eTag, content: JSON.parse(sent) },
      saved: (next?: string) => {
        savedJson.value = sent;
        unreadable.value = 0;
        if (next) eTag = next;
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
