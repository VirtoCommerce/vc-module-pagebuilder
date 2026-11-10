import { computed, ref } from "vue";
import * as api from "./api";
import type { PendingThemeFile } from "./designerSave";
import { rulesPath } from "./history";
import { formatRules, readRules, unreadableRules, type TemplateRules } from "./rules";

export function useTemplateRules() {
  const rules = ref<TemplateRules>(readRules(undefined));
  const savedJson = ref(formatRules(rules.value));
  const loaded = ref(false);
  /** The last read failed: until one succeeds the rules are not edited, because what is shown is not the file. */
  const failed = ref(false);
  const unreadable = ref(0);
  let at: { storeId: string; theme: string } | undefined;
  let eTag = "";
  const dirty = computed(() => loaded.value && formatRules(rules.value) !== savedJson.value);

  async function load(storeId: string, theme: string) {
    at = { storeId, theme };
    let file: Awaited<ReturnType<typeof api.loadThemeFile>>;
    try {
      file = await api.loadThemeFile(storeId, rulesPath(theme));
    } catch (e) {
      failed.value = !loaded.value;
      throw e;
    }
    failed.value = false;
    eTag = file.eTag;
    rules.value = readRules(file.content);
    unreadable.value = unreadableRules(file.content);
    savedJson.value = formatRules(rules.value);
    loaded.value = true;
  }

  function pending(): PendingThemeFile | undefined {
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

  async function reload() {
    if (at) await load(at.storeId, at.theme);
  }

  return { rules, dirty, loaded, failed, unreadable, load, pending, reload };
}

export type TemplateRulesState = ReturnType<typeof useTemplateRules>;
