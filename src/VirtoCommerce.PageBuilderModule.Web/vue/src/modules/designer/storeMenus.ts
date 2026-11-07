// A `menu` setting holds the name of one of the store's link lists (docs/contracts/theme-snapshot.md, "Menus"): the
// designer offers the lists the Content module keeps for the store, the ones its Menus blade edits.

/** The store's link lists (Content module, content:read) */
export const storeMenusUrl = (storeId: string) => `/api/cms/${encodeURIComponent(storeId)}/menu`;

export interface MenuOption {
  /** the list's name, which the setting holds */
  value: string;
  label: string;
  /** a name the store has no list of: kept so that it shows, to be replaced */
  unknown?: boolean;
}

const isText = (value: unknown): value is string => typeof value === "string" && !!value.trim();

/**
 * The options of a `menu` setting: each list name once (the storefront matches names ignoring case, and a list has one
 * per language), with its languages; the value set when the store has no list of that name
 */
export function menuOptions(lists: unknown, current?: unknown): MenuOption[] {
  const byName = new Map<string, { name: string; languages: Set<string> }>();
  for (const list of Array.isArray(lists) ? lists : []) {
    const { name, language } = (list ?? {}) as { name?: unknown; language?: unknown };
    if (!isText(name)) continue;
    const entry = byName.get(name.toLowerCase()) ?? { name, languages: new Set<string>() };
    if (isText(language)) entry.languages.add(language);
    byName.set(name.toLowerCase(), entry);
  }
  const options: MenuOption[] = [...byName.values()]
    .sort((a, b) => a.name.localeCompare(b.name))
    .map(({ name, languages }) => ({ value: name, label: languages.size ? `${name} (${[...languages].sort().join(", ")})` : name }));
  if (isText(current) && !byName.has(current.toLowerCase())) options.unshift({ value: current, label: current, unknown: true });
  return options;
}
