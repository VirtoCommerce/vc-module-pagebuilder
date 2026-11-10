const LINK_PARAMS = ["designGroup", "designPath", "designType", "cultureName"];

/** The classic admin's page blade links here, so these query parameters are a contract with it. */
export function designerFromLink(search: string): { name: string; param?: string; query?: Record<string, string> } | undefined {
  const params = new URLSearchParams(search);
  const storeId = params.get("storeId");
  const group = params.get("designGroup");
  const path = params.get("designPath");
  if (!storeId || (!group && !path)) return undefined;
  if (group) return { name: "PageBuilderDesigner", param: group };
  const query: Record<string, string> = { storeId, path: path!, type: params.get("designType") || "pages" };
  const cultureName = params.get("cultureName");
  if (cultureName) query.cultureName = cultureName;
  return { name: "PageBuilderDesigner", query };
}

/** The open designer is in the address from then on, so a reload restores it from there and one the user closed stays closed. */
export function forgetDesignerLink() {
  const url = new URL(window.location.href);
  for (const key of LINK_PARAMS) url.searchParams.delete(key);
  history.replaceState(history.state, "", url);
}
