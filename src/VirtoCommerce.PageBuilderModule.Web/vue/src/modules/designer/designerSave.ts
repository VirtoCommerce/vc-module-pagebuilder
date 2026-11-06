// One designer Save is one request (docs/contracts/history-api.md, "Save"): the page and the theme files, written as
// drafts all or nothing, and one version of the site's history.

/** The page part of a Save: a database page by its group and version, or a page kept as a file */
export type DesignerSavePage = { groupId: string; eTag: string; content: unknown } | { path: string; type: string; content: unknown };

export interface DesignerSaveFile {
  /** the theme folder and the file path: "default/sections/header-group.json" */
  path: string;
  content: unknown;
}

/** The save; theme files name the catalog of the storefront previewed, which the server checks them against */
export function designerSaveUrl(storeId: string, catalogHash?: string) {
  const query = new URLSearchParams({ storeId });
  if (catalogHash) query.set("catalogHash", catalogHash);
  return `/api/pagebuilder/designer/save?${query}`;
}

// sent as text: the server saves the bytes as they are, and the history tells the published version by them
const text = (content: unknown) => JSON.stringify(content, null, 2);

/** The request body: every content as the JSON text the server saves */
export function designerSaveBody(save: { page?: DesignerSavePage; themeFiles: DesignerSaveFile[] }) {
  return {
    page: save.page && { ...save.page, content: text(save.page.content) },
    themeFiles: save.themeFiles.map((x) => ({ path: x.path, content: text(x.content) })),
  };
}
