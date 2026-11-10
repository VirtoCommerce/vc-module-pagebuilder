import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { defineComponent } from "vue";
import { mount } from "@vue/test-utils";
import { usePageDesigner } from "../../src/modules/page-builder/composables/usePageDesigner";
import { fakePlatform, status, type FakePlatform } from "./support/fakePlatform";
import { notification } from "./support/vcShell";

const shell = vi.hoisted(() => ({ openBlade: vi.fn(), bladeRegistered: false }));

vi.mock("@vc-shell/framework", async (importOriginal) => {
  const original = await importOriginal<typeof import("./support/vcShell")>();
  return {
    ...original,
    useBlade: () => ({ ...original.useBlade(), openBlade: shell.openBlade }),
    getBladeConfig: (name: string) => (shell.bladeRegistered && name === "PageBuilderDesigner" ? { name } : undefined),
  };
});

const STORE = "B2B-store";
const CONTEXT = { groupId: "g1", storeId: STORE };
const SWITCH = "VirtoCommerce.PageBuilderModule.Store.NewPageBuilder";

function pageDesigner() {
  let open!: ReturnType<typeof usePageDesigner>;
  mount(
    defineComponent({
      setup() {
        open = usePageDesigner();
        return () => null;
      },
    }),
  );
  return open;
}

describe("opening the page designer", () => {
  let platform: FakePlatform;
  let windowOpen: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    platform = fakePlatform().install();
    windowOpen = vi.spyOn(window, "open").mockImplementation(() => null);
    shell.openBlade.mockClear();
    shell.bladeRegistered = false;
  });
  afterEach(() => windowOpen.mockRestore());

  it("opens the Angular designer for a store on it, and tells the caller nothing opened here", async () => {
    platform.on("GET", `/api/stores/${STORE}`, { languages: ["en-US"], settings: [] });
    const onOpen = vi.fn();

    await pageDesigner()(CONTEXT, { onOpen });

    expect(windowOpen).toHaveBeenCalledTimes(1);
    expect(String(windowOpen.mock.calls[0][0])).toContain("page-builder-designer/index.html");
    expect(shell.openBlade).not.toHaveBeenCalled();
    expect(onOpen).not.toHaveBeenCalled();
    expect(notification.error).not.toHaveBeenCalled();
  });

  it("opens the designer blade for a store on the New Page Builder, with the caller's open and close callbacks", async () => {
    platform.on("GET", `/api/stores/${STORE}`, { languages: ["en-US"], settings: [{ name: SWITCH, value: true }] });
    shell.bladeRegistered = true;
    const onOpen = vi.fn();
    const onClose = vi.fn();

    await pageDesigner()(CONTEXT, { onOpen, onClose });

    expect(windowOpen).not.toHaveBeenCalled();
    expect(shell.openBlade).toHaveBeenCalledWith({ name: "PageBuilderDesigner", param: "g1", onOpen, onClose });
  });

  it("opens no designer and shows an error when the store can't be read", async () => {
    platform.on("GET", `/api/stores/${STORE}`, status(500, "boom"));

    await pageDesigner()(CONTEXT);

    expect(windowOpen).not.toHaveBeenCalled();
    expect(notification.error).toHaveBeenCalledWith("The site couldn't be read, so the designer didn't open. Try again.");
  });
});
