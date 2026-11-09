import type { PageDocument } from "./types";
import { templateKeyOf } from "./templates";
import type { ThemeStatus } from "./themePublication";

// The theme's templates the preview's draft carries. The storefront takes the draft's templates in place of all the
// published ones, so the draft has every template of the theme or none: none while no template has a saved draft (the
// published snapshot holds them as they are live), else each template as last saved, its draft, else its live file.

/** The keys of the templates with a draft, from theme/status ("templates/product.json" → "product") */
export const draftedTemplateKeys = (status: ThemeStatus | undefined): string[] =>
  [...new Set((status?.drafts ?? []).map((x) => templateKeyOf(x)).filter((x): x is string => !!x))].sort();

/** Where the saved templates are read from */
export interface TemplateSource {
  /** every template of the theme, drafts included */
  keys: () => Promise<string[]>;
  /** a template as last saved, its draft, else its live file; undefined when the theme has none */
  read: (key: string) => Promise<PageDocument | undefined>;
}

/**
 * Reads the theme's saved templates for the draft: none while no template has a draft. Live files change only with a
 * publication, so those without a draft, and the list of templates, are read once per publication of the theme
 * (`scope` names the store and theme); the drafts on every call.
 */
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

/**
 * The draft's templates: the saved ones, the open template as the designer has it (saved or not) in place of its
 * saved one; none while the saved ones are (no template has a draft: the render carries the open one, and wins)
 */
export function draftTemplates(
  saved: Record<string, PageDocument> | undefined,
  open?: { key: string; document: PageDocument },
): Record<string, PageDocument> | undefined {
  if (!saved) return undefined;
  return open ? { ...saved, [open.key]: { settings: open.document.settings, content: open.document.content } } : saved;
}
