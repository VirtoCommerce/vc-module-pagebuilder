import type { PageDocument } from "./types";
import { templateKeyOf } from "./templates";
import type { ThemeStatus } from "./themePublication";

// The storefront takes the draft's templates in place of all the published ones, so a draft has every template or none.

export const draftedTemplateKeys = (status: ThemeStatus | undefined): string[] =>
  [...new Set((status?.drafts ?? []).map((x) => templateKeyOf(x)).filter((x): x is string => !!x))].sort();

export interface TemplateSource {
  keys: () => Promise<string[]>;
  read: (key: string) => Promise<PageDocument | undefined>;
}

/** Live files change only with a publication, so those without a draft are read once per publication. */
export function savedTemplatesReader() {
  let live: { scope: string; keys?: string[]; docs: Map<string, PageDocument | undefined> } | undefined;
  return async function read(scope: string, status: ThemeStatus, source: TemplateSource): Promise<Record<string, PageDocument> | undefined> {
    const drafted = draftedTemplateKeys(status);
    if (!drafted.length) return undefined;
    const at = `${scope}#${status.publication ?? ""}`;
    if (live?.scope !== at) live = { scope: at, docs: new Map() };
    const cache = live;
    cache.keys ??= await source.keys();
    const keys = [...new Set([...cache.keys, ...drafted])];
    const docs = await Promise.all(
      keys.map(async (key) => {
        if (drafted.includes(key)) return source.read(key);
        if (!cache.docs.has(key)) cache.docs.set(key, await source.read(key));
        return cache.docs.get(key);
      }),
    );
    const templates: Record<string, PageDocument> = {};
    keys.forEach((key, i) => {
      const doc = docs[i];
      if (doc) templates[key] = { settings: doc.settings, content: doc.content };
    });
    return templates;
  };
}

export function draftTemplates(
  saved: Record<string, PageDocument> | undefined,
  open?: { key: string; document: PageDocument },
): Record<string, PageDocument> | undefined {
  if (!saved) return undefined;
  return open ? { ...saved, [open.key]: { settings: open.document.settings, content: open.document.content } } : saved;
}
