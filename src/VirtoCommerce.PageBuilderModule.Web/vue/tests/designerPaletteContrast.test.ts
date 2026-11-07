import assert from "node:assert/strict";
import test from "node:test";
import {
  checkBody,
  checkPaletteUrl,
  failureCount,
  failureRows,
  formatRatio,
  generateBody,
  generatePaletteUrl,
  parseCheck,
  parseGenerated,
  undoStep,
  withGenerated,
  withStep,
  type PaletteFailure,
} from "../src/modules/designer/paletteContrast";
import { humanize } from "../src/modules/designer/schema";
import type { ThemeLook } from "../src/modules/designer/themeLook";

const look: ThemeLook = {
  brand: { logoUrl: "/logo.svg" },
  light: { color_primary_500: "#f99e24" },
  dark: { color_primary_500: "#ffbb5b" },
};

const failure = (over: Partial<PaletteFailure>): PaletteFailure => ({
  scheme: "light",
  pair: "button-solid-primary",
  text: "color_additional_50",
  background: "color_primary_500",
  textColor: "#ffffff",
  backgroundColor: "#f99e24",
  ratio: 2.11,
  minimumRatio: 4.5,
  ...over,
});

test("generated palettes replace both palettes at once and one undo brings both back", () => {
  const generated = { light: { color_primary_500: "#a25c27", color_neutral_50: "#f7f7f7" }, dark: { color_primary_500: "#f99e24" }, check: { failures: [], unchecked: 0 } };

  const applied = withGenerated(look, [], generated);
  assert.deepEqual(applied.look.light, generated.light);
  assert.deepEqual(applied.look.dark, generated.dark);
  assert.equal(applied.look.brand, look.brand);
  assert.equal(applied.steps.length, 1);
  // the answer is copied: editing the look later leaves it alone
  applied.look.light.color_primary_500 = "#000000";
  assert.equal(generated.light.color_primary_500, "#a25c27");

  // a brand edit made after the generation stays when it is undone
  const edited = { ...applied.look, brand: { logoUrl: "/new.svg" } };
  const undone = undoStep(edited, applied.steps);
  assert.ok(undone);
  assert.deepEqual(undone.look, { brand: { logoUrl: "/new.svg" }, light: look.light, dark: look.dark });
  assert.deepEqual(undone.steps, []);
  assert.equal(undoStep(undone.look, undone.steps), undefined);
});

test("each change is a step, the oldest go past the limit", () => {
  let steps = withStep([], look);
  // the step is a copy, not the look's own palettes
  look.light.color_primary_500 = "#123456";
  assert.equal(steps[0].light.color_primary_500, "#f99e24");
  look.light.color_primary_500 = "#f99e24";

  for (let i = 0; i < 60; i++) steps = withStep(steps, { ...look, light: { n: String(i) } });
  assert.equal(steps.length, 50);
  assert.equal(steps[steps.length - 1].light.n, "59");
  assert.equal(steps[0].light.n, "10");
});

test("failing pairs show one row per pair of tokens, the worst first, with the highest ratio required", () => {
  const failures = [
    failure({ pair: "link", text: "color_accent_600", background: "color_neutral_50", ratio: 4.1 }),
    failure({}),
    failure({ pair: "button-outline-primary", text: "color_primary_500", background: "color_additional_50" }),
    failure({ pair: "empty-list-icon", text: "color_primary_500", background: "color_additional_50", minimumRatio: 3 }),
    failure({ scheme: "dark", pair: "header-top-text", ratio: 1.5 }),
  ];

  const rows = failureRows(failures, "light");
  assert.deepEqual(
    rows.map((x) => [x.text, x.background, x.ratio, x.required, x.pairs]),
    [
      ["Additional 50", "Primary 500", "2.11:1", "4.5:1", ["Button solid primary"]],
      ["Primary 500", "Additional 50", "2.11:1", "4.5:1", ["Button outline primary", "Empty list icon"]],
      ["Accent 600", "Neutral 50", "4.1:1", "4.5:1", ["Link"]],
    ],
  );
  assert.equal(rows[0].textColor, "#ffffff");
  assert.equal(rows[0].backgroundColor, "#f99e24");
  assert.deepEqual(failureRows(failures, "dark").map((x) => x.pairs), [["Header top text"]]);
  assert.equal(failureCount(failures, "dark"), 1);
  assert.equal(failureCount(failures, "light"), 4);
});

test("ratios read as people write them", () => {
  assert.equal(formatRatio(4.5), "4.5:1");
  assert.equal(formatRatio(3), "3:1");
  assert.equal(formatRatio(2.11), "2.11:1");
  assert.equal(formatRatio(4.499), "4.5:1");
  assert.equal(humanize("mobile-menu-link-active"), "Mobile menu link active");
});

test("the requests carry the storefront's tokens; a second color only when there is one", () => {
  const tokens = { light: ["color_primary_500"], dark: ["color_primary_500"] };
  assert.deepEqual(generateBody(tokens, "#1b789b"), { primary: "#1b789b", tokens });
  assert.deepEqual(generateBody(tokens, "#1b789b", "#f99e24"), { primary: "#1b789b", secondary: "#f99e24", tokens });
  assert.deepEqual(checkBody(tokens, look), { palette: { light: look.light, dark: look.dark }, tokens });
  assert.equal(generatePaletteUrl("s 1"), "/api/pagebuilder/theme/palette?storeId=s+1");
  assert.equal(checkPaletteUrl("s1"), "/api/pagebuilder/theme/palette/check?storeId=s1");
});

test("answers are read defensively", () => {
  // the generator's colors are read as a palette file's, its failures as a check's
  assert.deepEqual(parseGenerated({ light: { a: "#fff", b: 1, c: " " }, failures: [failure({})] }), {
    light: { a: "#fff" },
    dark: {},
    check: { failures: [failure({})], unchecked: 0 },
  });
  assert.deepEqual(parseGenerated({ light: {}, failures: [], unchecked: 2 }).check, { failures: [], unchecked: 2 });
  assert.deepEqual(parseGenerated(null), { light: {}, dark: {}, check: { failures: [], unchecked: 0 } });
  assert.deepEqual(parseCheck({ failures: [failure({}), "x"], unchecked: 3 }), { failures: [failure({})], unchecked: 3 });
  assert.deepEqual(parseCheck({ unchecked: "3" }), { failures: [], unchecked: 0 });
});
