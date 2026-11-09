import assert from "node:assert/strict";
import test from "node:test";
import { displayText, isTranslations, translationOf, withTranslation, type Translating } from "../src/modules/designer/translatable";
import { itemTitle, withTextEdit } from "../src/modules/designer/schema";
import { hidingLosesMain, keptMainSection, mainSectionState, MAIN_SECTIONS } from "../src/modules/designer/templates";
import { runSearch, searchDisplayValue, requestResult, type SearchField } from "../src/modules/designer/search";
import {
  IMPERSONATE_PERMISSION,
  impersonateBody,
  previewUserIds,
  previewUsers,
  readToken,
  usableToken,
} from "../src/modules/designer/previewUsers";
import type { Field, Item, RemoteRequest } from "../src/modules/designer/types";

const en: Translating = { language: "en-US", defaultLanguage: "en-US" };
const de: Translating = { language: "de-DE", defaultLanguage: "en-US" };

// ── translatable settings ─────────────────────────────────────────────────────

test("a dictionary is culture names to texts, one of them with a subtag, as the server reads it", () => {
  assert.equal(isTranslations({ "en-US": "Hi", "de-DE": "Hallo" }), true);
  assert.equal(isTranslations({ "en-US": "Hi", "sr-Latn-RS": "Zdravo", de: "Hallo", "EN-gb": "Hello" }), true);
  // without a culture name with a subtag, a row such as a list's { sku } stays a row
  assert.equal(isTranslations({ en: "Hi" }), false);
  assert.equal(isTranslations({ sku: "X1" }), false);
  assert.equal(isTranslations({ "en-US": "Hi", title: "Hi" }), false);
  assert.equal(isTranslations({ "en-US": 1 }), false);
  assert.equal(isTranslations({}), false);
  assert.equal(isTranslations(["en-US"]), false);
  assert.equal(isTranslations("Hi"), false);
});

test("a language sees its own text, else the default language's, else the first; a plain string is the default's", () => {
  const dictionary = { "en-US": "Free shipping", "de-DE": "Kostenloser Versand" };
  assert.deepEqual(translationOf(dictionary, de), { own: "Kostenloser Versand", languages: ["en-US", "de-DE"] });
  assert.deepEqual(translationOf(dictionary, { ...de, language: "fr-FR" }), {
    fallback: { text: "Free shipping", language: "en-US" },
    languages: ["en-US", "de-DE"],
  });
  assert.deepEqual(translationOf({ "de-DE": "Hallo", "en-US": "Hi" }, { ...de, language: "fr-FR" }).fallback, { text: "Hi", language: "en-US" });
  assert.deepEqual(translationOf({ "de-DE": "Hallo" }, { ...de, language: "fr-FR" }).fallback, { text: "Hallo", language: "de-DE" });
  // the language is matched ignoring case, as the publication does
  assert.equal(translationOf({ "de-de": "Hallo" }, de).own, "Hallo");
  // without its own: its language part, else a culture of its language, before the default language
  const austrian = { ...de, language: "de-AT" };
  assert.deepEqual(translationOf({ "en-US": "Hi", "de-DE": "Hallo", de: "Servus" }, austrian).fallback, { text: "Servus", language: "de" });
  assert.deepEqual(translationOf({ "en-US": "Hi", "de-DE": "Hallo" }, austrian).fallback, { text: "Hallo", language: "de-DE" });
  assert.deepEqual(translationOf("Free shipping", en), { own: "Free shipping", languages: ["en-US"] });
  assert.deepEqual(translationOf("Free shipping", de), { fallback: { text: "Free shipping" }, languages: [] });
  assert.deepEqual(translationOf(undefined, de), { languages: [] });
});

test("another language's first text keeps the plain string as the default language's", () => {
  assert.deepEqual(withTranslation("Free shipping", de, "Kostenloser Versand"), { "en-US": "Free shipping", "de-DE": "Kostenloser Versand" });
  assert.deepEqual(withTranslation(undefined, de, "Hallo"), { "de-DE": "Hallo" });
  // the default language edits a plain string as it is
  assert.equal(withTranslation("Free shipping", en, "Free delivery"), "Free delivery");
  assert.deepEqual(withTranslation({ "en-US": "A", "de-DE": "B" }, en, "C"), { "en-US": "C", "de-DE": "B" });
  // the key the dictionary has, whatever its case
  assert.deepEqual(withTranslation({ "en-US": "A", "de-de": "B" }, de, "C"), { "en-US": "A", "de-de": "C" });
});

test("an emptied language falls back again; the default language's text alone is a plain string again", () => {
  assert.equal(withTranslation({ "en-US": "A", "de-DE": "B" }, de, ""), "A");
  assert.deepEqual(withTranslation({ "en-US": "A", "de-DE": "B", "fr-FR": "C" }, de, ""), { "en-US": "A", "fr-FR": "C" });
  assert.deepEqual(withTranslation({ "de-DE": "B", "fr-FR": "C" }, de, ""), { "fr-FR": "C" });
  assert.equal(withTranslation({ "de-DE": "B" }, de, ""), "");
  assert.deepEqual(withTranslation({ "en-US": "A", "de-DE": "B" }, en, ""), { "de-DE": "B" });
});

test("a label shows a dictionary's default language text", () => {
  assert.equal(displayText({ "de-DE": "Hallo", "en-US": "Hi" }, "en-US"), "Hi");
  assert.equal(displayText({ "de-DE": "Hallo", "en-US": "Hi" }), "Hallo");
  assert.equal(displayText("Hi"), "Hi");
  assert.equal(displayText(3), undefined);
  assert.equal(itemTitle({ id: "a", type: "announcement", text: { "en-US": "Sale" } }, { displayField: "text", name: "Announcement" }), "Sale");
});

const announcement: Field[] = [
  { id: "text", type: "string", translatable: true },
  { id: "link", type: "string" },
  { id: "links", type: "list", element: [{ id: "label", type: "string", translatable: true }] },
  { id: "body", type: "text", translatable: true },
];

test("an edit in the preview writes the language's text of a translatable setting, only where the item is edited per language", () => {
  const item: Item = { id: "a", type: "announcement", text: "Sale", link: "/sale", links: [{ label: "More" }], body: "<p>x</p>" };
  assert.deepEqual(withTextEdit(item, announcement, ["text"], "Angebot", de)?.text, { "en-US": "Sale", "de-DE": "Angebot" });
  assert.deepEqual(withTextEdit(item, announcement, ["links", 0, "label"], "Mehr", de)?.links, [{ label: { "en-US": "More", "de-DE": "Mehr" } }]);
  // a page's own section (no language edited): a plain text, as before
  assert.equal(withTextEdit(item, announcement, ["text"], "Angebot")?.text, "Angebot");
  // not translatable: the text, in whichever language
  assert.equal(withTextEdit(item, announcement, ["link"], "/angebot", de)?.link, "/angebot");
  // a dictionary is edited per language even where the schema forgot to say so
  const dictionary = { ...item, link: { "en-US": "/sale" } };
  assert.deepEqual(withTextEdit(dictionary, announcement, ["link"], "/angebot", de)?.link, { "en-US": "/sale", "de-DE": "/angebot" });
});

test("an edit in the preview never adds a setting, edits rich text, or edits a dictionary as plain text", () => {
  const item: Item = { id: "a", type: "announcement", text: { "en-US": "Sale" }, body: "<p>x</p>" };
  assert.equal(withTextEdit(item, announcement, ["body"], "y", de), undefined);
  assert.equal(withTextEdit(item, announcement, ["link"], "/x", de), undefined);
  assert.equal(withTextEdit(item, announcement, ["text"], "Plain"), undefined);
  assert.equal(withTextEdit(item, announcement, ["missing"], "x", de), undefined);
});

// ── the template's main section ───────────────────────────────────────────────

const section = (id: string, type: string, extra: Partial<Item> = {}): Item => ({ id, type, ...extra });

test("every page type has its main section", () => {
  assert.deepEqual(MAIN_SECTIONS, { product: "product-info", category: "category-listing", page: "page-content" });
});

test("a template's main section is shown, hidden or missing; the header's sections don't count", () => {
  assert.equal(mainSectionState([section("a", "text"), section("m", "product-info")], "product"), "shown");
  assert.equal(mainSectionState([section("m", "product-info", { hidden: true })], "product"), "hidden");
  assert.equal(mainSectionState([section("m", "product-info", { __group: "header" })], "product"), "missing");
  assert.equal(mainSectionState([section("m", "page-content")], "category"), "missing");
  assert.equal(mainSectionState([section("m", "category-listing")], undefined), undefined);
});

test("removing the last main section is refused; one of two may go, and so may the other sections", () => {
  const content = [section("t", "text"), section("m", "category-listing"), section("h", "category-listing", { hidden: true })];
  assert.equal(keptMainSection(content, "category", ["t"]), undefined);
  assert.equal(keptMainSection(content, "category", ["m"]), undefined);
  assert.equal(keptMainSection(content, "category", ["m", "h", "t"]), "m");
  assert.equal(keptMainSection([section("m", "category-listing", { hidden: true })], "category", ["m"]), "m");
  // a page is not a template
  assert.equal(keptMainSection(content, undefined, ["m", "h"]), undefined);
  // a template without one has none to keep
  assert.equal(keptMainSection([section("t", "text")], "page", ["t"]), undefined);
});

test("hiding the last shown main section is refused; showing it, or hiding another, is not", () => {
  const content = [section("m", "page-content"), section("h", "page-content", { hidden: true }), section("t", "text")];
  assert.equal(hidingLosesMain(content, "page", "m"), true);
  assert.equal(hidingLosesMain(content, "page", "h"), false);
  assert.equal(hidingLosesMain(content, "page", "t"), false);
  assert.equal(hidingLosesMain([...content, section("n", "page-content")], "page", "m"), false);
  assert.equal(hidingLosesMain(content, undefined, "m"), false);
});

// ── the search field ──────────────────────────────────────────────────────────

const request = (url: string, result?: string): RemoteRequest => ({ url, ...(result ? { response: { result } } : {}) });

test("a search runs its request with the query and keeps the answer as the value", async () => {
  const seen: Record<string, unknown>[] = [];
  const field: SearchField = { id: "product", type: "search", request: request("/api/x?q={{__searchQuery}}") };
  const value = await runSearch(field, { model: { a: 1 } }, "sku-1", async (_request, context) => {
    seen.push(context);
    return { name: "Printer" };
  });
  assert.deepEqual(value, { __nodata: false, __searchQuery: "sku-1", value: { name: "Printer" } });
  assert.equal(seen[0].__searchQuery, "sku-1");
  assert.deepEqual(seen[0].model, { a: 1 });
  assert.deepEqual(await runSearch(field, {}, "none", async () => null), { __nodata: true, __searchQuery: "none", value: null });
});

test("several requests run in turn, each seeing the answers so far, each answer under its key", async () => {
  const field: SearchField = { id: "s", type: "search", requests: { product: request("/p"), price: request("/q") } };
  const items: unknown[] = [];
  const value = await runSearch(field, {}, null, async (req, context) => {
    items.push(context.item);
    return req.url === "/p" ? { id: "p1" } : undefined;
  });
  assert.deepEqual(value, { __nodata: false, __searchQuery: null, product: { id: "p1" }, price: undefined });
  assert.deepEqual(items, [{ __nodata: true, __searchQuery: null }, { __nodata: false, __searchQuery: null, product: { id: "p1" } }]);
  await assert.rejects(runSearch(field, {}, "x", async () => Promise.reject(new Error("403"))));
});

test("a search shows the answer's values by their dotted paths; a request's answer is at its result path", () => {
  const value = { __nodata: false, __searchQuery: "x", value: { name: "Printer", images: [{ url: "/p.png" }] } };
  assert.equal(searchDisplayValue(value, { label: "Name", path: "name" }), "Printer");
  assert.equal(searchDisplayValue(value, { label: "Image", path: "images.0.url", type: "image" }), "/p.png");
  assert.equal(searchDisplayValue(value, { label: "None" }), undefined);
  assert.deepEqual(requestResult(request("/x", "data.product"), { data: { product: { id: 1 } } }), { id: 1 });
  assert.deepEqual(requestResult(request("/x"), [1]), [1]);
});

// ── preview as a customer ─────────────────────────────────────────────────────

test("the store lists the accounts to preview as, one id per line; the grant needs login on behalf", () => {
  const store = { settings: [{ name: "VirtoCommerce.PageBuilderModule.Store.PreviewUserIds", value: " u1\r\nu2\n\nu1\n" }] };
  assert.deepEqual(previewUserIds(store), ["u1", "u2"]);
  assert.deepEqual(previewUserIds({ settings: [] }), []);
  assert.deepEqual(previewUserIds(undefined), []);
  assert.equal(IMPERSONATE_PERMISSION, "platform:security:loginOnBehalf");
  assert.deepEqual(previewUsers(["u1", "u2", "u3"], [{ id: "u2", email: "b@x" }, { id: "u1", userName: "anna" }]), [
    { id: "u1", name: "anna" },
    { id: "u2", name: "b@x" },
    { id: "u3", name: "u3" },
  ]);
});

test("the impersonate grant asks for no refresh token, and the preview gets the access token alone", () => {
  const body = new URLSearchParams(impersonateBody("user 1&x"));
  assert.equal(body.get("grant_type"), "impersonate");
  assert.equal(body.get("user_id"), "user 1&x");
  assert.equal(body.has("scope"), false);
  const token = readToken("u1", { access_token: "jwt", refresh_token: "r", expires_in: 1800 }, 1000);
  assert.deepEqual(token, { userId: "u1", accessToken: "jwt", expiresAt: 1000 + 1800 * 1000 });
  assert.equal(readToken("u1", { refresh_token: "r" }, 0), undefined);
});

test("a token is used again for its user until a minute before it expires", () => {
  const token = { userId: "u1", accessToken: "jwt", expiresAt: 600_000 };
  assert.equal(usableToken(token, "u1", 500_000), token);
  assert.equal(usableToken(token, "u1", 540_001), undefined);
  assert.equal(usableToken(token, "u2", 0), undefined);
  assert.equal(usableToken(undefined, "u1", 0), undefined);
});
