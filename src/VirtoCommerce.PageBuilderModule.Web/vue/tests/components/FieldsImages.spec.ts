import { describe, expect, it, vi } from "vitest";
import { flushPromises, mount, type VueWrapper } from "@vue/test-utils";
import Fields from "../../src/modules/designer/components/Fields.vue";
import { PickAssetKey, type Field } from "../../src/modules/designer/types";

const LIST: Field = { id: "images", type: "images", multiple: true, label: "Images" };
const SINGLE: Field = { id: "image", type: "images", multiple: false, label: "Image" };

function mountFields(field: Field, value: unknown, pickAsset = async (_acceptedTypes?: string[]) => "new.png") {
  return mount(Fields, {
    props: { fields: [field], values: { [field.id!]: value }, context: {} },
    global: { provide: { [PickAssetKey as symbol]: pickAsset } },
  });
}

const lastValue = (wrapper: VueWrapper) => wrapper.emitted("update")!.at(-1)![1];
const urls = (wrapper: VueWrapper) => wrapper.findAll("input").map((x) => (x.element as HTMLInputElement).value);
const button = (wrapper: VueWrapper, label: string, index = 0) => wrapper.findAll(`button[aria-label="${label}"]`)[index];
const buttonByText = (wrapper: VueWrapper, text: string, index = 0) => wrapper.findAll("button").filter((x) => x.text() === text)[index];

describe("an images field with several images", () => {
  it("shows every image of the list", () => {
    expect(urls(mountFields(LIST, ["a.png", "b.png"]))).toEqual(["a.png", "b.png"]);
  });

  it("shows the url of an image kept as an asset object", () => {
    expect(urls(mountFields(LIST, [{ url: "a.png", name: "a" }]))).toEqual(["a.png"]);
  });

  it("adds a picked asset at the end, keeping the others", async () => {
    const wrapper = mountFields(LIST, ["a.png", "b.png"]);

    await buttonByText(wrapper, "Add").trigger("click");
    await flushPromises();

    expect(lastValue(wrapper)).toEqual(["a.png", "b.png", "new.png"]);
  });

  it("starts a list from an empty value", async () => {
    const wrapper = mountFields(LIST, undefined);

    await buttonByText(wrapper, "Add").trigger("click");
    await flushPromises();

    expect(lastValue(wrapper)).toEqual(["new.png"]);
  });

  it("keeps an image saved as one url when another is added", async () => {
    const wrapper = mountFields(LIST, "a.png");

    await buttonByText(wrapper, "Add").trigger("click");
    await flushPromises();

    expect(lastValue(wrapper)).toEqual(["a.png", "new.png"]);
  });

  it("replaces only the image picked again", async () => {
    const wrapper = mountFields(LIST, ["a.png", "b.png"]);

    await buttonByText(wrapper, "Select", 1).trigger("click");
    await flushPromises();

    expect(lastValue(wrapper)).toEqual(["a.png", "new.png"]);
  });

  it("keeps the row while its url is being retyped", async () => {
    const wrapper = mountFields(LIST, ["a.png", "b.png"]);

    await wrapper.findAll("input")[0].setValue("");

    expect(lastValue(wrapper)).toEqual(["", "b.png"]);
  });

  it("removes only the image asked for", async () => {
    const wrapper = mountFields(LIST, ["a.png", "b.png"]);

    await button(wrapper, "Remove", 0).trigger("click");

    expect(lastValue(wrapper)).toEqual(["b.png"]);
  });

  it("moves an image, an asset object staying as it was", async () => {
    const wrapper = mountFields(LIST, [{ url: "a.png", name: "a" }, "b.png"]);

    await button(wrapper, "Move down", 0).trigger("click");

    expect(lastValue(wrapper)).toEqual(["b.png", { url: "a.png", name: "a" }]);
  });

  it("does not move the first image up or the last one down", () => {
    const wrapper = mountFields(LIST, ["a.png", "b.png"]);

    expect(button(wrapper, "Move up", 0).attributes("disabled")).toBeDefined();
    expect(button(wrapper, "Move down", 1).attributes("disabled")).toBeDefined();
  });
});

describe("an images field with one image", () => {
  it("stays one url", async () => {
    const wrapper = mountFields(SINGLE, "a.png");

    await buttonByText(wrapper, "Select").trigger("click");
    await flushPromises();

    expect(lastValue(wrapper)).toBe("new.png");
  });
});

describe("the asset types a field opens the picker on", () => {
  async function pickedTypes(field: Field, button: string, index = 0) {
    const pickAsset = vi.fn(async (_acceptedTypes?: string[]) => "new.png");
    const wrapper = mountFields(field, field.multiple ? ["a.png"] : "a.png", pickAsset);

    await buttonByText(wrapper, button, index).trigger("click");
    await flushPromises();

    return pickAsset.mock.calls[0][0];
  }

  it("is images for an images field without accept", async () => {
    expect(await pickedTypes(SINGLE, "Select")).toEqual(["image/*"]);
  });

  it("is the field's accept list, trimmed", async () => {
    expect(await pickedTypes({ ...SINGLE, accept: ".svg, image/png ," }, "Select")).toEqual([".svg", "image/png"]);
  });

  it("is any type for a files field without accept", async () => {
    expect(await pickedTypes({ id: "file", type: "files", label: "File" }, "Select")).toEqual([]);
  });

  it("is the accept list of a files field", async () => {
    expect(await pickedTypes({ id: "file", type: "files", label: "File", accept: ".pdf,application/pdf" }, "Select")).toEqual([".pdf", "application/pdf"]);
  });

  it("is the same when a list adds an image or replaces one", async () => {
    const field: Field = { ...LIST, accept: "image/png" };

    expect(await pickedTypes(field, "Add")).toEqual(["image/png"]);
    expect(await pickedTypes(field, "Select")).toEqual(["image/png"]);
  });
});
