import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { mount } from "@vue/test-utils";
import Fields from "../../src/modules/designer/components/Fields.vue";
import type { Field } from "../../src/modules/designer/types";
import { fakePlatform, type FakePlatform } from "./support/fakePlatform";

const searchField = (request: Field["request"]): Field => ({ id: "found", type: "search", button: true, request });

async function pressSearch(field: Field) {
  const wrapper = mount(Fields, { props: { fields: [field], values: {}, context: {} } });
  await wrapper.find("button").trigger("click");
  return wrapper;
}

describe("a search field's request", () => {
  let platform: FakePlatform;

  beforeEach(() => {
    platform = fakePlatform().install();
    vi.spyOn(console, "warn").mockImplementation(() => undefined);
  });

  afterEach(() => vi.mocked(console.warn).mockRestore());

  it("sends a GraphQL read to the platform", async () => {
    platform.on("POST", "/graphql", { data: { a: 1 } });

    const wrapper = await pressSearch(searchField({ url: "/graphql", method: "post", body: { query: "{ a }" }, response: { result: "data.a" } }));

    await vi.waitFor(() => expect(wrapper.emitted("update")?.length).toBe(1));
    const [request] = platform.to("/graphql", "POST");
    expect(request.body).toEqual({ query: "{ a }" });
  });

  it("sends nothing for a mutation, and says the search failed", async () => {
    const wrapper = await pressSearch(searchField({ url: "/graphql", method: "POST", body: { query: "mutation { a }" } }));

    await vi.waitFor(() => expect(wrapper.text()).toContain("The search failed."));
    expect(platform.fetch).not.toHaveBeenCalled();
  });

  it("checks the query as filled in from the context", async () => {
    const body = { query: '{ a(q: "{{injected}}") }' };
    const wrapper = mount(Fields, {
      props: { fields: [searchField({ url: "/graphql", method: "POST", body })], values: {}, context: { injected: '") } mutation { b(c: "' } },
    });

    await wrapper.find("button").trigger("click");

    await vi.waitFor(() => expect(wrapper.text()).toContain("The search failed."));
    expect(platform.fetch).not.toHaveBeenCalled();
  });

  it("sends nothing to another platform path", async () => {
    const wrapper = await pressSearch(searchField({ url: "/api/platform/security/users", method: "POST", body: { a: 1 } }));

    await vi.waitFor(() => expect(wrapper.text()).toContain("The search failed."));
    expect(platform.fetch).not.toHaveBeenCalled();
  });

  it("goes to another origin without the platform's credentials", async () => {
    const schema = "https://elsewhere.example/schema.json";
    platform.on("GET", schema, { a: 1 });

    const wrapper = await pressSearch(searchField({ url: schema }));

    await vi.waitFor(() => expect(wrapper.emitted("update")?.length).toBe(1));
    const sent = platform.to(schema);
    expect(sent).toHaveLength(1);
    expect(sent[0].headers.authorization).toBeUndefined();
    expect(sent[0].credentials).not.toBe("include");
  });
});
