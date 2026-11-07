import type { DesignerSaveFile } from "./designerSave";
import { THEME_FILES, type ThemePart } from "./history";
import { isRecord, isText } from "./json";
import { humanize } from "./schema";

// The theme's brand and palette (docs/contracts/theme-snapshot.md, "Brand" and "Palette"): config/brand.json and the light
// and dark palette files, edited as drafts, saved with the designer's Save and published with the theme.

/** The logos and the favicon, by their white-labeling names */
export interface Brand {
  /** the logo on light backgrounds: the header in the light theme */
  logoUrl?: string;
  /** the logo on dark backgrounds: the footer, and the header in the dark theme */
  secondaryLogoUrl?: string;
  faviconUrl?: string;
}

export type BrandField = keyof Brand;

export const BRAND_FIELDS: BrandField[] = ["logoUrl", "secondaryLogoUrl", "faviconUrl"];

/** A palette: colors by token; a token left out keeps the storefront preset's color */
export interface Palette {
  light: Record<string, string>;
  /** without it, the storefront has no dark theme */
  dark?: Record<string, string>;
}

/** The brand and both palettes as the Theme tab edits them */
export interface ThemeLook {
  brand: Brand;
  light: Record<string, string>;
  dark: Record<string, string>;
}

export type LookPart = keyof ThemeLook;

/** The theme part each part of the look is saved as */
export const LOOK_FILES: Readonly<Record<LookPart, ThemePart>> = { brand: "brand", light: "palette", dark: "darkPalette" };

const LOOK_PARTS = Object.keys(LOOK_FILES) as LookPart[];

/** The theme settings that hold the logos before the theme has a brand file */
const SETTINGS_LOGOS: Record<BrandField, string> = {
  logoUrl: "logo_image",
  secondaryLogoUrl: "logo_inverted_image",
  faviconUrl: "favicon_image",
};

/** The brand of the brand file; a theme without one starts from the logos of its settings */
export function readBrand(file: unknown, settings: Record<string, unknown>): Brand {
  const source = isRecord(file) ? file : undefined;
  return Object.fromEntries(
    BRAND_FIELDS.map((field) => [field, source ? source[field] : settings[SETTINGS_LOGOS[field]]]).filter(([, value]) => isText(value)),
  ) as Brand;
}

/** A palette's colors, by token, as a palette file or the generator holds them */
export const readColors = (file: unknown): Record<string, string> =>
  isRecord(file) ? (Object.fromEntries(Object.entries(file).filter(([, value]) => isText(value))) as Record<string, string>) : {};

/** The brand without its empty fields, as the brand file holds it */
export const brandFile = (brand: Brand): Brand =>
  Object.fromEntries(BRAND_FIELDS.map((field) => [field, brand[field]?.trim()]).filter(([, value]) => !!value)) as Brand;

const same = (a: unknown, b: unknown) => JSON.stringify(a) === JSON.stringify(b);

/** A part as its file holds it: the brand without its empty fields, a palette as it is */
const fileContent = (look: ThemeLook, part: LookPart) => (part === "brand" ? brandFile(look.brand) : look[part]);

/** The files Save writes: those of the parts changed since `saved` */
export function lookFiles(theme: string, look: ThemeLook, saved: ThemeLook): { part: LookPart; file: DesignerSaveFile }[] {
  return LOOK_PARTS.filter((part) => !same(fileContent(look, part), fileContent(saved, part))).map((part) => ({
    part,
    file: { path: THEME_FILES[LOOK_FILES[part]](theme), content: fileContent(look, part) },
  }));
}

/**
 * What the preview shows in place of the published brand and palette. An empty brand is left out, so the published one
 * shows; a palette is sent only with light colors, as a publication keeps one only then; a dark part only with dark
 * colors.
 */
export function lookDraft(look: ThemeLook): { brand?: Brand; palette?: Palette } {
  const brand = brandFile(look.brand);
  const hasLight = Object.keys(look.light).length > 0;
  const hasDark = Object.keys(look.dark).length > 0;
  return {
    ...(Object.keys(brand).length ? { brand } : {}),
    ...(hasLight ? { palette: { light: look.light, ...(hasDark ? { dark: look.dark } : {}) } } : {}),
  };
}

export interface PaletteRow {
  token: string;
  value?: string;
  /** a token the storefront announced; one only the file has is shown so that it can be cleared */
  known: boolean;
}

export interface PaletteFamily {
  /** "primary" for color_primary_50 … color_primary_950; "" for tokens without a shade */
  family: string;
  rows: PaletteRow[];
}

const COLOR_PREFIX = /^color_/;
const SHADE = /_(\d+)$/;

/** A token's family: its name without the prefix and the shade */
export const tokenFamily = (token: string) => (SHADE.test(token) ? token.replace(COLOR_PREFIX, "").replace(SHADE, "") : "");

/** A token as people read it: "color_primary_500" → "Primary 500" */
export const tokenLabel = (token: string): string => humanize(token.replace(COLOR_PREFIX, "")) || token;

/** The tokens the storefront announced, then those only the file has, in families, in the order they came */
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
  // the tokens without a shade last
  return [...families].sort(([a], [b]) => Number(!a) - Number(!b)).map(([family, familyRows]) => ({ family, rows: familyRows }));
}

export interface TokenOption {
  value: string;
  label: string;
  /** the color the theme's light palette gives it, for a swatch */
  color?: string;
  /** a value the storefront does not announce: kept so that it shows, to be replaced */
  unknown?: boolean;
}

/** What a `color-token` setting may hold: the storefront's light palette tokens; a value it lacks shows as unknown */
export function tokenOptions(tokens: string[], colors: Record<string, string>, current?: unknown): TokenOption[] {
  const options: TokenOption[] = tokens.map((token) => ({ value: token, label: tokenLabel(token), ...(colors[token] ? { color: colors[token] } : {}) }));
  if (isText(current) && !tokens.includes(current)) options.unshift({ value: current, label: current, unknown: true });
  return options;
}
