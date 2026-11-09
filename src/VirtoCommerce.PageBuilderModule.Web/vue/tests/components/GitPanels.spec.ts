import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { enableAutoUnmount, flushPromises, mount, type VueWrapper } from "@vue/test-utils";
import PageBuilderGit from "../../src/modules/pages/PageBuilderGit.vue";
import PageBuilderGitHub from "../../src/modules/pages/PageBuilderGitHub.vue";
import { blade } from "./support/vcShell";
import { fakePlatform, type FakePlatform } from "./support/fakePlatform";

const CONNECTION = "/api/pagebuilder/git/connection";
const APP_SETUP = "/api/pagebuilder/git/app/setup";

const refused = () => new Response(JSON.stringify({ error: "Refused by the platform" }), { status: 403 });
const alertText = (wrapper: VueWrapper) => wrapper.find("[role=alert]").text();
const organizationField = (wrapper: VueWrapper) => wrapper.find("input");
const createButton = (wrapper: VueWrapper) => wrapper.find("button[data-icon='lucide-github']");

async function focusWindow() {
  window.dispatchEvent(new Event("focus"));
  await flushPromises();
}

let platform: FakePlatform;

// the panel listens on the window: one left mounted would answer the next test's focus
enableAutoUnmount(afterEach);

beforeEach(() => {
  vi.stubGlobal("defineBlade", () => undefined);
  vi.stubGlobal("open", vi.fn(() => ({ close: vi.fn(), location: { href: "" }, opener: null })));
  blade.param.value = "s1";
  platform = fakePlatform().install();
});

describe("a site's history panel", () => {
  it("says why when its first request fails", async () => {
    platform.on("GET", "/api/pagebuilder/git/site", refused);

    const wrapper = mount(PageBuilderGit);
    await flushPromises();

    expect(alertText(wrapper)).toContain("Refused by the platform");
  });
});

describe("the GitHub panel", () => {
  async function mountPanel() {
    const wrapper = mount(PageBuilderGitHub);
    await flushPromises();
    return wrapper;
  }

  async function failToCreateApp(wrapper: VueWrapper) {
    await organizationField(wrapper).setValue("acme");
    await createButton(wrapper).trigger("click");
    await flushPromises();
  }

  it("says why when its first request fails", async () => {
    platform.on("GET", CONNECTION, refused);

    expect(alertText(await mountPanel())).toContain("Refused by the platform");
  });

  it("keeps the reason a Create App failed when the window gets the focus back", async () => {
    platform.on("GET", CONNECTION, { configured: false }).on("GET", APP_SETUP, refused);
    const wrapper = await mountPanel();

    await failToCreateApp(wrapper);
    await focusWindow();

    expect(alertText(wrapper)).toContain("Refused by the platform");
  });

  it("reads the connection again when the window gets the focus back", async () => {
    platform.on("GET", CONNECTION, { configured: false });
    await mountPanel();
    const before = platform.to(CONNECTION).length;

    await focusWindow();

    expect(platform.to(CONNECTION).length).toBe(before + 1);
  });

  describe("set up again", () => {
    const CONFIGURED = { configured: true, source: "app", sites: [] };

    async function mountSettingUp() {
      platform.on("GET", CONNECTION, CONFIGURED);
      const wrapper = await mountPanel();
      await wrapper.findAll("button").find((x) => x.text() === "Set up again")!.trigger("click");
      expect(organizationField(wrapper).exists()).toBe(true);
      return wrapper;
    }

    it("keeps the form when the editor comes back from another tab", async () => {
      const wrapper = await mountSettingUp();

      await focusWindow();

      expect(organizationField(wrapper).exists()).toBe(true);
    });

    it("closes the form when the editor comes back from creating the App", async () => {
      platform.on("GET", APP_SETUP, { url: "https://github.test/new" });
      const wrapper = await mountSettingUp();
      await failToCreateApp(wrapper);

      await focusWindow();

      expect(organizationField(wrapper).exists()).toBe(false);
    });
  });
});
