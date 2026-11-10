import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { flushPromises, mount, type VueWrapper } from "@vue/test-utils";
import { effectScope } from "vue";
import ThemePanel from "../../src/modules/designer/components/ThemePanel.vue";
import { PickAssetKey } from "../../src/modules/designer/types";
import { PALETTE_CHECK_DEBOUNCE_MS, type PaletteFailure } from "../../src/modules/designer/paletteContrast";
import { useThemeLook } from "../../src/modules/designer/useThemeLook";
import { fakePlatform, themeFileAnswer, type FakePlatform } from "./support/fakePlatform";

const STORE = "B2B-store";
const TOKENS = { light: ["color_primary_500", "color_neutral_50"], dark: ["color_primary_500"] };

const failure = (text: string): PaletteFailure => ({
  scheme: "light",
  pair: "button-solid-primary",
  text,
  background: "color_primary_500",
  textColor: "#ffffff",
  backgroundColor: "#f99e24",
  ratio: 2.11,
  minimumRatio: 4.5,
});

let platform: FakePlatform;
let scope: ReturnType<typeof effectScope>;

beforeEach(() => {
  vi.useFakeTimers();
  platform = fakePlatform()
    .on("GET", "/api/pagebuilder/designer/theme-file", themeFileAnswer({ "default/config/palette.json": { color_primary_500: "#f99e24" } }))
    .on("POST", "/api/pagebuilder/theme/palette/check", { failures: [failure("color_neutral_50")], unchecked: 1 })
    .on("POST", "/api/pagebuilder/theme/palette", {
      light: { color_primary_500: "#a25c27", color_neutral_50: "#f7f7f7" },
      dark: { color_primary_500: "#f99e24" },
      failures: [failure("color_vc_text_solid_primary")],
    })
    .install();
  scope = effectScope();
});

afterEach(() => {
  scope.stop();
  vi.useRealTimers();
});

async function mountPanel(provide: Record<symbol, unknown> = {}) {
  const look = scope.run(() => useThemeLook())!;
  await look.load(STORE, "default");
  const wrapper = mount(ThemePanel, { props: { look, storeId: STORE, paletteTokens: TOKENS }, global: { provide } });
  await settle();
  return { wrapper, look };
}

async function settle() {
  await vi.advanceTimersByTimeAsync(PALETTE_CHECK_DEBOUNCE_MS + 10);
  await flushPromises();
}

const checks = () => platform.to("/api/pagebuilder/theme/palette/check", "POST");
const failingTexts = (wrapper: VueWrapper) => wrapper.findAll(".pd-theme__pair-text > span:first-child").map((x) => x.text());

describe("ThemePanel: palette contrast", () => {
  it("checks the palettes once they are loaded", async () => {
    const { wrapper } = await mountPanel();

    expect(checks()).toHaveLength(1);
    expect(failingTexts(wrapper)).toEqual(["Neutral 50 on Primary 500"]);
  });

  it("shows what the generator found right away, then checks the generated palettes for the pairs it can't; an edit after it is checked", async () => {
    const { wrapper, look } = await mountPanel();

    await wrapper.findAll("button").find((x) => x.text() === "Generate")!.trigger("click");
    await flushPromises();
    expect(platform.to("/api/pagebuilder/theme/palette", "POST")).toHaveLength(1);
    expect(look.look.value.light.color_primary_500).toBe("#a25c27");
    expect(checks()).toHaveLength(1);
    expect(failingTexts(wrapper)).toEqual(["Vc text solid primary on Primary 500"]);

    await settle();
    expect(checks()).toHaveLength(2);
    expect(failingTexts(wrapper)).toEqual(["Neutral 50 on Primary 500"]);

    look.setColor("light", "color_neutral_50", "#ffffff");
    await settle();
    expect(checks()).toHaveLength(3);
  });
});

describe("ThemePanel: brand images", () => {
  it("opens the picker on images for the logos and the favicon", async () => {
    const pickAsset = vi.fn(async (_acceptedTypes?: string[]) => undefined);
    const { wrapper } = await mountPanel({ [PickAssetKey as symbol]: pickAsset });

    for (const select of wrapper.findAll("button").filter((x) => x.text() === "Select")) await select.trigger("click");
    await flushPromises();

    expect(pickAsset.mock.calls.map(([types]) => types)).toEqual([["image/*"], ["image/*"], ["image/*"]]);
  });
});
