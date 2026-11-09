import { describe, expect, it } from "vitest";
import { mount, type VueWrapper } from "@vue/test-utils";
import DiscardDialog from "../../src/modules/designer/components/DiscardDialog.vue";
import { discardOptions } from "../../src/modules/designer/discard";
import { PERMISSIONS } from "../../src/modules/designer/permissions";

const only =
  (...granted: string[]) =>
  (permission: string) =>
    granted.includes(permission);

function mountDialog(props: { page?: boolean; theme?: Parameters<typeof discardOptions>[0]["theme"]; template?: boolean; unsaved?: boolean; granted?: string[] } = {}) {
  const template = props.template ?? false;
  const options = discardOptions({
    page: props.page ?? true,
    theme: props.theme ?? ["rules", "header", "brand"],
    template,
    hasAccess: only(...(props.granted ?? [PERMISSIONS.update, PERMISSIONS.theme])),
  });
  return mount(DiscardDialog, { props: { options, template, unsaved: props.unsaved ?? false } });
}

const option = (wrapper: VueWrapper, text: string) => {
  const found = wrapper.findAll("label.pd-discard__option").find((x) => x.text() === text);
  if (!found) throw new Error(`no option "${text}"`);
  return found;
};
const box = (wrapper: VueWrapper, text: string) => option(wrapper, text).find("input").element as HTMLInputElement;
const discardButton = (wrapper: VueWrapper) => wrapper.findAll("button").find((x) => x.text() === "Discard")!;

describe("DiscardDialog", () => {
  it("lists the page's draft ticked and each theme part's unticked, saying theme drafts are shared by every page", () => {
    const wrapper = mountDialog();

    expect(wrapper.findAll("label.pd-discard__option").map((x) => x.text())).toEqual(["Page", "Rules", "Header", "Brand"]);
    expect(box(wrapper, "Page").checked).toBe(true);
    expect(["Rules", "Header", "Brand"].map((x) => box(wrapper, x).checked)).toEqual([false, false, false]);
    expect(wrapper.text()).toContain("Theme drafts are shared by every page");
  });

  it("lets only the parts the editor may edit be ticked", () => {
    const wrapper = mountDialog();

    expect(box(wrapper, "Rules").disabled).toBe(true);
    expect(option(wrapper, "Rules").attributes("title")).toBe("You don't have permission to change this.");
    expect(box(wrapper, "Header").disabled).toBe(false);
  });

  it("sends the page alone unless theme parts are ticked", async () => {
    const wrapper = mountDialog();

    await discardButton(wrapper).trigger("click");
    await option(wrapper, "Header").find("input").trigger("change");
    await discardButton(wrapper).trigger("click");

    expect(wrapper.emitted("discard")).toEqual([[{ page: true, theme: [] }], [{ page: true, theme: ["header"] }]]);
  });

  it("names an open template's draft as the template; one the editor may not edit is not ticked", () => {
    const wrapper = mountDialog({ template: true, theme: [] });

    expect(wrapper.findAll("label.pd-discard__option").map((x) => x.text())).toEqual(["Template"]);
    expect(box(wrapper, "Template").checked).toBe(false);
    expect(box(wrapper, "Template").disabled).toBe(true);
    expect(wrapper.text()).not.toContain("Theme drafts are shared by every page");
  });

  it("discards nothing until something is ticked, unless there are unsaved changes, which go anyway", async () => {
    const wrapper = mountDialog();
    await option(wrapper, "Page").find("input").trigger("change");
    expect(discardButton(wrapper).attributes("disabled")).toBeDefined();

    const unsaved = mountDialog({ unsaved: true });
    await option(unsaved, "Page").find("input").trigger("change");
    expect(unsaved.text()).toContain("Your unsaved changes are discarded too.");
    await discardButton(unsaved).trigger("click");
    expect(unsaved.emitted("discard")).toEqual([[{ page: false, theme: [] }]]);
  });
});
