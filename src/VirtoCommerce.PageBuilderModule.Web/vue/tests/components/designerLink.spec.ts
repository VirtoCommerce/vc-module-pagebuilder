import { afterEach, describe, expect, it } from "vitest";
import { designerFromLink, forgetDesignerLink } from "../../standalone/designerLink";

const STORE = "B2B-store";

/** What the standalone app does on each page load. */
function loadApp() {
  const designer = designerFromLink(window.location.search);
  if (designer) forgetDesignerLink();
  return designer;
}

afterEach(() => history.replaceState(null, "", "/"));

describe("the classic admin's link to the standalone designer", () => {
  it("opens the file page it names once, so a reload restores it from the address instead of opening another", () => {
    history.replaceState({ position: 3 }, "", `/apps/page-builder-shell/?storeId=${STORE}&designType=pages&designPath=about.page&cultureName=de-DE#/page-builder-draft`);

    expect(loadApp()).toEqual({ name: "PageBuilderDesigner", query: { storeId: STORE, path: "about.page", type: "pages", cultureName: "de-DE" } });
    expect(window.location.pathname).toBe("/apps/page-builder-shell/");
    expect(window.location.search).toBe(`?storeId=${STORE}`);
    expect(window.location.hash).toBe("#/page-builder-draft");
    expect(history.state).toEqual({ position: 3 });

    expect(loadApp()).toBeUndefined();
  });

  it("opens the page group it names once", () => {
    history.replaceState(null, "", `/apps/page-builder-shell/?storeId=${STORE}&designGroup=g1`);

    expect(loadApp()).toEqual({ name: "PageBuilderDesigner", param: "g1" });
    expect(window.location.search).toBe(`?storeId=${STORE}`);
    expect(loadApp()).toBeUndefined();
  });

  it("opens nothing without a store", () => {
    history.replaceState(null, "", "/apps/page-builder-shell/?designGroup=g1");

    expect(loadApp()).toBeUndefined();
  });
});
