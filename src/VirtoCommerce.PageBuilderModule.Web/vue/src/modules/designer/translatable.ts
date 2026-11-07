// Translatable settings (docs/contracts/theme-snapshot.md, "Translatable fields"): in the theme's files a setting whose
// schema field has `"translatable": true` holds a string, the same in every language, or a dictionary from language
// to text. The publication resolves the dictionaries; the preview's draft shows the text of its language.

/** A language name with a subtag ("en-US"), as the server reads the keys of a dictionary */
const LANGUAGE = /^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})+$/;

export type Translations = Record<string, string>;

/** The language the settings panel edits translatable settings in, the store default language, the store's languages */
export interface Translating {
  language: string;
  defaultLanguage: string;
  languages?: string[];
}

/** A dictionary: every key a language name, every value a text */
export function isTranslations(value: unknown): value is Translations {
  if (!value || typeof value !== "object" || Array.isArray(value)) return false;
  const entries = Object.entries(value);
  return entries.length > 0 && entries.every(([key, text]) => LANGUAGE.test(key) && typeof text === "string");
}

const keyOf = (translations: Translations, language: string) =>
  language in translations ? language : Object.keys(translations).find((x) => x.toLowerCase() === language.toLowerCase());

export interface Translation {
  /** the language's own text; none: it shows `fallback` */
  own?: string;
  /** what the language shows without a text of its own, and from which language (none: a plain string, for all) */
  fallback?: { text: string; language?: string };
  /** the languages with a text of their own, the edited one included */
  languages: string[];
}

/**
 * A translatable setting as the language sees it. A plain string is the default language's text and every other
 * language's fallback; a dictionary falls back to the default language's text, else its first, as the publication does.
 */
export function translationOf(value: unknown, at: Translating): Translation {
  if (isTranslations(value)) {
    const own = keyOf(value, at.language);
    const fallbackKey = keyOf(value, at.defaultLanguage) ?? Object.keys(value)[0];
    return {
      ...(own ? { own: value[own] } : {}),
      ...(!own ? { fallback: { text: value[fallbackKey], language: fallbackKey } } : {}),
      languages: Object.keys(value),
    };
  }
  const text = typeof value === "string" ? value : "";
  if (at.language === at.defaultLanguage) return { own: text, languages: text ? [at.language] : [] };
  return { ...(text ? { fallback: { text } } : {}), languages: [] };
}

/**
 * The setting with the language's text. A plain string becomes a dictionary once another language gets a text of its
 * own, keeping the string as the default language's; an empty text removes the language's own; a dictionary left
 * with the default language's text alone is a plain string again.
 */
export function withTranslation(value: unknown, at: Translating, text: string): unknown {
  if (!isTranslations(value) && at.language === at.defaultLanguage) return text;
  const translations: Translations = isTranslations(value)
    ? { ...value }
    : typeof value === "string" && value
      ? { [at.defaultLanguage]: value }
      : {};
  const key = keyOf(translations, at.language) ?? at.language;
  if (text) translations[key] = text;
  else delete translations[key];
  const keys = Object.keys(translations);
  if (!keys.length) return "";
  if (keys.length === 1 && keys[0].toLowerCase() === at.defaultLanguage.toLowerCase()) return translations[keys[0]];
  return translations;
}

/** A label's text: a plain string, else a dictionary's default language text, else its first */
export function displayText(value: unknown, defaultLanguage?: string): string | undefined {
  if (typeof value === "string") return value;
  if (!isTranslations(value)) return undefined;
  const key = (defaultLanguage && keyOf(value, defaultLanguage)) || Object.keys(value)[0];
  return value[key];
}
