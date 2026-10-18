import { computed, ref } from "vue";
import * as api from "./api";
import { rulesPath } from "./history";
import { formatRules, rulesFromFile, type TemplateRules } from "./rules";

/**
 * The store's template rules as the designer edits them: the theme's rules file as last saved (its draft, else what is
 * live; no file: the storefront's default). Save writes a draft; the template's Publish puts it live with the template.
 */
export function useTemplateRules() {
  const rules = ref<TemplateRules>(rulesFromFile(undefined));
  const savedJson = ref(formatRules(rules.value));
  const loaded = ref(false);
  /** a saved draft the site doesn't use yet */
  const hasDraft = ref(false);
  let at: { storeId: string; theme: string } | undefined;
  const dirty = computed(() => loaded.value && formatRules(rules.value) !== savedJson.value);

  async function load(storeId: string, theme: string) {
    at = { storeId, theme };
    const path = rulesPath(theme);
    const [file, drafts] = await Promise.all([api.loadThemeFile(storeId, theme, path), api.themeFileDrafts(storeId, [path]).catch(() => [])]);
    rules.value = rulesFromFile(file);
    savedJson.value = formatRules(rules.value);
    hasDraft.value = drafts.length > 0;
    loaded.value = true;
  }

  async function save() {
    if (!at || !dirty.value) return;
    const sent = formatRules(rules.value);
    await api.saveThemeFiles(at.storeId, at.theme, [{ path: rulesPath(at.theme), content: JSON.parse(sent) }]);
    savedJson.value = sent;
    hasDraft.value = true;
  }

  async function publish() {
    if (!at || !hasDraft.value) return;
    await api.publishThemeFiles(at.storeId, [rulesPath(at.theme)]);
    hasDraft.value = false;
  }

  /** Throws the saved draft away (a never-published file keeps it: it is the only copy) and the unsaved edits */
  async function discard() {
    if (!at) return;
    if (hasDraft.value) await api.discardBlobDraft(at.storeId, "themes", rulesPath(at.theme), at.theme).catch((e) => e instanceof api.ApiError && e.status === 409 ? undefined : Promise.reject(e));
    await load(at.storeId, at.theme);
  }

  return { rules, dirty, hasDraft, loaded, load, save, publish, discard };
}

export type TemplateRulesState = ReturnType<typeof useTemplateRules>;
