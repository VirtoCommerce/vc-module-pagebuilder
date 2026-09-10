import assert from "node:assert/strict";
import test from "node:test";
import * as appLocales from "../src/locales";
import * as sharedComponentLocales from "../src/modules/shared-components/locales";

const expectedLocaleCodes = ["de", "en", "es", "fi", "fr", "it", "ja", "no", "pl", "pt", "ru", "sv", "zh"];

function flattenMessages(value: unknown, prefix = ""): Map<string, string> {
  const messages = new Map<string, string>();

  if (!value || typeof value !== "object" || Array.isArray(value)) {
    throw new TypeError(`Expected a locale object at ${prefix || "<root>"}.`);
  }

  for (const [key, child] of Object.entries(value)) {
    const path = prefix ? `${prefix}.${key}` : key;
    if (typeof child === "string") {
      messages.set(path, child);
    } else {
      for (const [childPath, message] of flattenMessages(child, path)) {
        messages.set(childPath, message);
      }
    }
  }

  return messages;
}

function getPlaceholders(message: string): string[] {
  return [...message.matchAll(/\{([^{}]+)\}/g)].map((match) => match[1]).sort();
}

test("shared components provides a dedicated dictionary for every app locale", () => {
  const appLocaleCodes = Object.keys(appLocales).sort();
  const sharedComponentLocaleCodes = Object.keys(sharedComponentLocales).sort();

  assert.deepEqual(appLocaleCodes, expectedLocaleCodes);
  assert.deepEqual(sharedComponentLocaleCodes, appLocaleCodes);

  for (const localeCode of sharedComponentLocaleCodes) {
    if (localeCode !== "en") {
      assert.notStrictEqual(
        sharedComponentLocales[localeCode as keyof typeof sharedComponentLocales],
        sharedComponentLocales.en,
        `${localeCode} must not alias the English dictionary`,
      );
      assert.notDeepEqual(
        sharedComponentLocales[localeCode as keyof typeof sharedComponentLocales],
        sharedComponentLocales.en,
        `${localeCode} must contain translated messages`,
      );
    }
  }
});

test("shared component locale keys and placeholders match English", () => {
  const englishMessages = flattenMessages(sharedComponentLocales.en);
  const englishKeys = [...englishMessages.keys()].sort();

  for (const [localeCode, locale] of Object.entries(sharedComponentLocales)) {
    const messages = flattenMessages(locale);
    assert.deepEqual([...messages.keys()].sort(), englishKeys, `${localeCode} locale keys differ from English`);

    for (const key of englishKeys) {
      assert.deepEqual(
        getPlaceholders(messages.get(key) ?? ""),
        getPlaceholders(englishMessages.get(key) ?? ""),
        `${localeCode}.${key} placeholders differ from English`,
      );
    }
  }
});

test("English messages use Shared Component terminology", () => {
  const englishMessages = flattenMessages(sharedComponentLocales.en);
  const legacyTerminology = new RegExp(String.raw`\b${["linked", "components?"].join(" ")}\b`, "i");

  assert.equal(englishMessages.get("SHARED_COMPONENTS.MENU.TITLE"), "Shared Components");
  for (const message of englishMessages.values()) {
    assert.doesNotMatch(message, legacyTerminology);
  }
});

test("shared component count messages have explicit singular and plural variants", () => {
  const countMessagePairs = [
    ["SHARED_COMPONENTS.DETAILS.USED_ON_ONE", "SHARED_COMPONENTS.DETAILS.USED_ON_MANY"],
    ["SHARED_COMPONENTS.DETAILS.DELETE_BLOCKED_ONE", "SHARED_COMPONENTS.DETAILS.DELETE_BLOCKED_MANY"],
    ["SHARED_COMPONENTS.NOTIFICATIONS.DELETE_BLOCKED_ONE", "SHARED_COMPONENTS.NOTIFICATIONS.DELETE_BLOCKED_MANY"],
  ] as const;

  for (const [localeCode, locale] of Object.entries(sharedComponentLocales)) {
    const messages = flattenMessages(locale);

    for (const [singularKey, pluralKey] of countMessagePairs) {
      const singular = messages.get(singularKey) ?? "";
      const plural = messages.get(pluralKey) ?? "";

      assert.notEqual(singular, plural, `${localeCode}.${singularKey} must differ from its plural variant`);
      assert.doesNotMatch(singular, /\{count\}/, `${localeCode}.${singularKey} must be explicitly singular`);
      assert.match(plural, /\{count\}/, `${localeCode}.${pluralKey} must interpolate the plural count`);
    }
  }
});
