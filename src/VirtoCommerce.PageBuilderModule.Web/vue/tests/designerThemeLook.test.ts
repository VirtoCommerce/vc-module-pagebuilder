import assert from "node:assert/strict";
import test from "node:test";
import {
  brandFile,
  brandPath,
  lookDraft,
  lookFiles,
  paletteFamilies,
  palettePath,
  readBrand,
  readColors,
  tokenFamily,
  tokenLabel,
  tokenOptions,
  type ThemeLook,
} from "../src/modules/designer/themeLook";
import { menuOptions, storeMenusUrl } from "../src/modules/designer/storeMenus";
import { brandField, isOverridden, menuField } from "../src/modules/designer/whiteLabeling";

const look = (part: Partial<ThemeLook> = {}): ThemeLook => ({ brand: {}, light: {}, dark: {}, ...part });

// ── brand and palette files ──────────────────────────────────────────────────

test("the brand is the brand file's; a theme without one starts from the logos of its settings", () => {
  const settings = { logo_image: "/l.svg", logo_inverted_image: "/d.svg", favicon_image: " ", other: "x" };
  assert.deepEqual(readBrand({ logoUrl: "/a.svg", secondaryLogoUrl: "", faviconUrl: "/f.ico", extra: 1 }, settings), { logoUrl: "/a.svg", faviconUrl: "/f.ico" });
  assert.deepEqual(readBrand(undefined, settings), { logoUrl: "/l.svg", secondaryLogoUrl: "/d.svg" });
  // an empty brand file is a brand without logos, not a missing one
  assert.deepEqual(readBrand({}, settings), {});
  assert.deepEqual(readBrand([], {}), {});
});

test("a palette file's colors are its texts by token", () => {
  assert.deepEqual(readColors({ color_primary_500: "#f99e24", radius: 4, color_x: "" }), { color_primary_500: "#f99e24" });
  assert.deepEqual(readColors(undefined), {});
  assert.deepEqual(readColors(["#fff"]), {});
});

test("Save writes the files of the parts changed: the brand without its empty fields, each palette whole", () => {
  const saved = look({ brand: { logoUrl: "/a.svg" }, light: { color_primary_500: "#111111" } });
  assert.deepEqual(lookFiles("default", saved, saved), []);
  assert.deepEqual(lookFiles("default", look({ ...saved, brand: { logoUrl: " /a.svg ", faviconUrl: "" } }), saved), []);

  const edited = look({ brand: { logoUrl: "/b.svg", faviconUrl: "/f.ico" }, light: saved.light, dark: { color_primary_500: "#222222" } });
  assert.deepEqual(lookFiles("default", edited, saved), [
    { path: "default/config/brand.json", content: { logoUrl: "/b.svg", faviconUrl: "/f.ico" } },
    { path: "default/config/palette.dark.json", content: { color_primary_500: "#222222" } },
  ]);
  // clearing every light color writes an empty palette: the theme then has none
  assert.deepEqual(lookFiles("default", look({ brand: saved.brand }), saved), [{ path: "default/config/palette.json", content: {} }]);
  assert.equal(brandPath("default"), "default/config/brand.json");
  assert.equal(palettePath("default", "light"), "default/config/palette.json");
  assert.deepEqual(brandFile({ logoUrl: "", secondaryLogoUrl: " /d.svg" }), { secondaryLogoUrl: "/d.svg" });
});

test("the preview gets the brand always, a palette only with light colors, its dark part only with dark ones", () => {
  assert.deepEqual(lookDraft(look({ brand: { logoUrl: "/a.svg", faviconUrl: "" } })), { brand: { logoUrl: "/a.svg" } });
  assert.deepEqual(lookDraft(look({ light: { a: "#111111" } })), { brand: {}, palette: { light: { a: "#111111" } } });
  assert.deepEqual(lookDraft(look({ light: { a: "#111111" }, dark: { a: "#222222" } })), {
    brand: {},
    palette: { light: { a: "#111111" }, dark: { a: "#222222" } },
  });
  // a dark palette without a light one is not published, so the preview does not show it either
  assert.deepEqual(lookDraft(look({ dark: { a: "#222222" } })), { brand: {} });
});

// ── palette tokens ───────────────────────────────────────────────────────────

test("tokens read as words, in families by their name without the shade", () => {
  assert.equal(tokenLabel("color_primary_500"), "Primary 500");
  assert.equal(tokenLabel("color_header_top_bg"), "Header top bg");
  assert.equal(tokenFamily("color_vc_text_solid_600"), "vc_text_solid");
  assert.equal(tokenFamily("color_header_top_bg"), "");
});

test("the palette editor lists the storefront's tokens with the theme's colors, then the file's others, shadeless last", () => {
  const families = paletteFamilies(["color_primary_50", "color_link", "color_primary_500", "color_neutral_50"], {
    color_primary_500: "#f99e24",
    color_old_100: "#000000",
  });
  assert.deepEqual(
    families.map((f) => [f.family, f.rows.map((r) => [r.token, r.value ?? null, r.known])]),
    [
      ["primary", [["color_primary_50", null, true], ["color_primary_500", "#f99e24", true]]],
      ["neutral", [["color_neutral_50", null, true]]],
      ["old", [["color_old_100", "#000000", false]]],
      ["", [["color_link", null, true]]],
    ],
  );
});

test("a color-token setting offers the light tokens with the theme's colors, and keeps a value the storefront lacks", () => {
  const tokens = ["color_neutral_900", "color_primary_500"];
  assert.deepEqual(tokenOptions(tokens, { color_primary_500: "#f99e24" }, "color_primary_500"), [
    { value: "color_neutral_900", label: "Neutral 900" },
    { value: "color_primary_500", label: "Primary 500", color: "#f99e24" },
  ]);
  // a hex typed before: shown, to be replaced by a token
  assert.deepEqual(tokenOptions(tokens, {}, "#ff0000")[0], { value: "#ff0000", label: "#ff0000", unknown: true });
  assert.equal(tokenOptions(tokens, {}, undefined).length, 2);
  assert.deepEqual(tokenOptions([], {}, undefined), []);
});

// ── store menus ──────────────────────────────────────────────────────────────

test("a menu setting offers each store link list once by its name, with its languages", () => {
  assert.equal(storeMenusUrl("B2B store"), "/api/cms/B2B%20store/menu");
  const lists = [
    { id: "2", name: "main menu", language: "de-DE" },
    { id: "1", name: "Main menu", language: "en-US", menuLinks: [] },
    { id: "3", name: "Footer", language: null },
    { id: "4", name: " " },
    null,
  ];
  assert.deepEqual(menuOptions(lists, "Main menu"), [
    { value: "Footer", label: "Footer" },
    { value: "main menu", label: "main menu (de-DE, en-US)" },
  ]);
  // the storefront matches names ignoring case: a value in another case is the list
  assert.equal(menuOptions(lists, "MAIN MENU").length, 2);
  assert.deepEqual(menuOptions(lists, "Gone")[0], { value: "Gone", label: "Gone", unknown: true });
  // the Content module answers an empty body for a store without lists
  assert.deepEqual(menuOptions(undefined), []);
});

// ── white-labeling ───────────────────────────────────────────────────────────

test("a field white-labeling sets for the visitor is marked: the logos, the palette, the menus of the group", () => {
  const overridden = ["brand.logoUrl", "palette", "menus.footer"];
  assert.equal(isOverridden(overridden, brandField("logoUrl")), true);
  assert.equal(isOverridden(overridden, brandField("faviconUrl")), false);
  assert.equal(isOverridden(overridden, "palette"), true);
  assert.equal(isOverridden(overridden, menuField("footer")), true);
  assert.equal(isOverridden(overridden, menuField("header")), false);
  // a page's own section has no group: white-labeling does not replace its menus
  assert.equal(isOverridden([...overridden, "menus.header"], menuField(undefined)), false);
  assert.equal(isOverridden(undefined, "palette"), false);
});
