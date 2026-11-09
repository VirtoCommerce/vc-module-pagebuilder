import { isText } from "./json";

export const storeMenusUrl = (storeId: string) => `/api/cms/${encodeURIComponent(storeId)}/menu`;

export interface MenuOption {
  value: string;
  label: string;
  unknown?: boolean;
}

/** One option per name: the storefront matches names ignoring case, and a list has one entry per language. */
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
