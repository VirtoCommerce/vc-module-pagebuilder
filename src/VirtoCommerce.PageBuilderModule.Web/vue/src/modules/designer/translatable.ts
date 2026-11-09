// In theme files a translatable setting holds one string for every language, or a dictionary from language to text.

const LANGUAGE = /^[a-z]{2,3}(-[a-z0-9]{2,8})*$/i;
/** A dictionary has a key with a subtag, so that a row such as `{ "sku": "X1" }` is not taken for one. */
const LANGUAGE_WITH_SUBTAG = /^[a-z]{2,3}(-[a-z0-9]{2,8})+$/i;

export type Translations = Record<string, string>;

export interface Translating {
  language: string;
  defaultLanguage: string;
  languages?: string[];
}

export function isTranslations(value: unknown): value is Translations {
  if (!value || typeof value !== "object" || Array.isArray(value)) return false;
  const entries = Object.entries(value);
  return entries.some(([key]) => LANGUAGE_WITH_SUBTAG.test(key)) && entries.every(([key, text]) => LANGUAGE.test(key) && typeof text === "string");
}

const keyOf = (translations: Translations, language: string) =>
  language in translations ? language : Object.keys(translations).find((x) => x.toLowerCase() === language.toLowerCase());

const languagePart = (language: string) => language.split("-")[0].toLowerCase();

/** Must pick the key the publication picks. */
function fallbackKeyOf(translations: Translations, language: string, defaultLanguage: string): string {
  const part = languagePart(language);
  const keys = Object.keys(translations);
  return keyOf(translations, part) ?? keys.find((x) => languagePart(x) === part) ?? keyOf(translations, defaultLanguage) ?? keys[0];
}

export interface Translation {
  own?: string;
  fallback?: { text: string; language?: string };
  languages: string[];
}

export function translationOf(value: unknown, at: Translating): Translation {
  if (isTranslations(value)) {
    const own = keyOf(value, at.language);
    const fallbackKey = fallbackKeyOf(value, at.language, at.defaultLanguage);
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

export function resolveTranslations<T>(value: T, language: string, defaultLanguage: string): T {
  if (isTranslations(value)) return value[keyOf(value, language) ?? fallbackKeyOf(value, language, defaultLanguage)] as T;
  if (Array.isArray(value)) return value.map((x) => resolveTranslations(x, language, defaultLanguage)) as T;
  if (value && typeof value === "object") {
    return Object.fromEntries(Object.entries(value).map(([key, x]) => [key, resolveTranslations(x, language, defaultLanguage)])) as T;
  }
  return value;
}

export function displayText(value: unknown, defaultLanguage?: string): string | undefined {
  if (typeof value === "string") return value;
  if (!isTranslations(value)) return undefined;
  const key = (defaultLanguage && keyOf(value, defaultLanguage)) || Object.keys(value)[0];
  return value[key];
}
