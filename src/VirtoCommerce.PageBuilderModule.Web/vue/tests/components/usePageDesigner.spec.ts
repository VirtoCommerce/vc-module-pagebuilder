import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { defineComponent } from "vue";
import { mount } from "@vue/test-utils";
import { usePageDesigner } from "../../src/modules/page-builder/composables/usePageDesigner";
import { fakePlatform, status, type FakePlatform } from "./support/fakePlatform";
import { notification } from "./support/vcShell";

const STORE = "B2B-store";
const CONTEXT = { groupId: "g1", storeId: STORE };

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
  });
  afterEach(() => windowOpen.mockRestore());

  it("opens the Angular designer for a store on it", async () => {
    platform.on("GET", `/api/stores/${STORE}`, { languages: ["en-US"], settings: [] });

    await pageDesigner()(CONTEXT);

    expect(windowOpen).toHaveBeenCalledTimes(1);
    expect(String(windowOpen.mock.calls[0][0])).toContain("page-builder-designer/index.html");
    expect(notification.error).not.toHaveBeenCalled();
  });

  it("opens no designer and shows an error when the store can't be read", async () => {
    platform.on("GET", `/api/stores/${STORE}`, status(500, "boom"));

    await pageDesigner()(CONTEXT);

    expect(windowOpen).not.toHaveBeenCalled();
    expect(notification.error).toHaveBeenCalledWith("The site couldn't be read, so the designer didn't open. Try again.");
  });
});
