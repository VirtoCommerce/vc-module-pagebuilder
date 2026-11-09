import type { DesignerSaveFile } from "./designerSave";
import { THEME_FILES, type ThemePart } from "./history";
import { isRecord, isText } from "./json";
import { humanize } from "./schema";

export interface Brand {
  /** The logo on light backgrounds: the header in the light theme. */
  logoUrl?: string;
  /** The logo on dark backgrounds: the footer, and the header in the dark theme. */
  secondaryLogoUrl?: string;
  faviconUrl?: string;
}

export type BrandField = keyof Brand;

export const BRAND_FIELDS: BrandField[] = ["logoUrl", "secondaryLogoUrl", "faviconUrl"];

/** A token left out keeps the storefront preset's color. */
export interface Palette {
  light: Record<string, string>;
  /** Without it, the storefront has no dark theme. */
  dark?: Record<string, string>;
}

export interface ThemeLook {
  brand: Brand;
  light: Record<string, string>;
  dark: Record<string, string>;
}

export type LookPart = keyof ThemeLook;

export const LOOK_FILES: Readonly<Record<LookPart, ThemePart>> = { brand: "brand", light: "palette", dark: "darkPalette" };

const LOOK_PARTS = Object.keys(LOOK_FILES) as LookPart[];

/** Read only for the logos of a theme without a brand file, and never written. */
export const settingsDataPath = (theme: string) => `${theme}/config/settings_data.json`;

const SETTINGS_LOGOS: Record<BrandField, string> = {
  logoUrl: "logo_image",
  secondaryLogoUrl: "logo_inverted_image",
  faviconUrl: "favicon_image",
};

export function currentSettings(data: unknown): Record<string, unknown> {
  if (!isRecord(data)) return {};
  const current = typeof data.current === "string" && isRecord(data.presets) ? data.presets[data.current] : data.current;
  if (!isRecord(current)) return {};
  return typeof data.current === "string" && isRecord(current.settings) ? current.settings : current;
}

export function readBrand(file: unknown, settingsData?: unknown): Brand {
  const source = isRecord(file) ? file : undefined;
  const settings = source ? {} : currentSettings(settingsData);
  return Object.fromEntries(
    BRAND_FIELDS.map((field) => [field, source ? source[field] : settings[SETTINGS_LOGOS[field]]]).filter(([, value]) => isText(value)),
  ) as Brand;
}

export const readColors = (file: unknown): Record<string, string> =>
  isRecord(file) ? (Object.fromEntries(Object.entries(file).filter(([, value]) => isText(value))) as Record<string, string>) : {};

export const brandFile = (brand: Brand): Brand =>
  Object.fromEntries(BRAND_FIELDS.map((field) => [field, brand[field]?.trim()]).filter(([, value]) => !!value)) as Brand;

const same = (a: unknown, b: unknown) => JSON.stringify(a) === JSON.stringify(b);

const fileContent = (look: ThemeLook, part: LookPart) => (part === "brand" ? brandFile(look.brand) : look[part]);

export function lookFiles(theme: string, look: ThemeLook, saved: ThemeLook): { part: LookPart; file: Omit<DesignerSaveFile, "eTag"> }[] {
  return LOOK_PARTS.filter((part) => !same(fileContent(look, part), fileContent(saved, part))).map((part) => ({
    part,
    file: { path: THEME_FILES[LOOK_FILES[part]](theme), content: fileContent(look, part) },
  }));
}

/**
 * An empty brand is left out so that the published one shows, unless `emptyBrand` says it is the theme's own. A palette
 * without light colors is null, because a publication then keeps none.
 */
export function lookDraft(look: ThemeLook, emptyBrand = false): { brand?: Brand; palette: Palette | null } {
  const brand = brandFile(look.brand);
  const hasLight = Object.keys(look.light).length > 0;
  const hasDark = Object.keys(look.dark).length > 0;
  return {
    ...(Object.keys(brand).length || emptyBrand ? { brand } : {}),
    palette: hasLight ? { light: look.light, ...(hasDark ? { dark: look.dark } : {}) } : null,
  };
}

export interface PaletteRow {
  token: string;
  value?: string;
  known: boolean;
}

export interface PaletteFamily {
  family: string;
  rows: PaletteRow[];
}

const COLOR_PREFIX = /^color_/;
const SHADE = /_(\d+)$/;

export const tokenFamily = (token: string) => (SHADE.test(token) ? token.replace(COLOR_PREFIX, "").replace(SHADE, "") : "");

export const tokenLabel = (token: string): string => humanize(token.replace(COLOR_PREFIX, "")) || token;

export function paletteFamilies(tokens: string[], values: Record<string, string>): PaletteFamily[] {
  const rows: PaletteRow[] = [
    ...tokens.map((token) => ({ token, value: values[token], known: true })),
    ...Object.keys(values)
      .filter((token) => !tokens.includes(token))
      .map((token) => ({ token, value: values[token], known: false })),
  ];
  const families = new Map<string, PaletteRow[]>();
  for (const row of rows) {
    const family = tokenFamily(row.token);
    families.set(family, [...(families.get(family) ?? []), row]);
  }
  return [...families].sort(([a], [b]) => Number(!a) - Number(!b)).map(([family, familyRows]) => ({ family, rows: familyRows }));
}

export interface TokenOption {
  value: string;
  label: string;
  color?: string;
  unknown?: boolean;
}

export function tokenOptions(tokens: string[], colors: Record<string, string>, current?: unknown): TokenOption[] {
  const options: TokenOption[] = tokens.map((token) => ({ value: token, label: tokenLabel(token), ...(colors[token] ? { color: colors[token] } : {}) }));
  if (isText(current) && !tokens.includes(current)) options.unshift({ value: current, label: current, unknown: true });
  return options;
}
