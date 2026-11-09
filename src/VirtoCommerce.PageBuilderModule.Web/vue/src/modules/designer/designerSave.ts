import type { DesignerDiscardRequest, DesignerSaveRequest } from "../../api_client/virtocommerce.pagebuildermodule";

// One designer Save is one request (docs/contracts/history-api.md, "Save"): the page and the theme files, written as
// drafts all or nothing, and one version of the site's history. A Discard is one request too.

/** The page part of a Save: a database page by its group and version, or a page kept as a file */
export type DesignerSavePage = { groupId: string; eTag: string; content: unknown } | { path: string; type: string; content: unknown };

export interface DesignerSaveFile {
  /** the theme folder and the file path: "default/sections/header-group.json" */
  path: string;
  /** the version the file was read at (loadThemeFile): 412 when someone saved it since */
  eTag: string;
  content: unknown;
}

/** Theme files go with the catalog of the storefront previewed, which the server checks them against: none before its handshake */
export class CatalogPendingError extends Error {
  constructor() {
    super("Theme files are saved with the catalog of the storefront previewed, which has not loaded yet.");
  }
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
export function designerSaveBody(save: { page?: DesignerSavePage; themeFiles: DesignerSaveFile[] }): DesignerSaveRequest {
  return {
    page: save.page && { ...save.page, content: text(save.page.content) },
    themeFiles: save.themeFiles.map((x) => ({ path: x.path, eTag: x.eTag, content: text(x.content) })),
  };
}

/** The page part of a Discard: a database page by its group, or a page kept as a file */
export type DesignerDiscardPage = { groupId: string } | { path: string; type: string };

export const designerDiscardUrl = (storeId: string) => `/api/pagebuilder/designer/discard?${new URLSearchParams({ storeId })}`;

/** The request body: the page and the theme files whose drafts go, by their path with the theme folder */
export function designerDiscardBody(discard: { page?: DesignerDiscardPage; themeFiles: string[] }): DesignerDiscardRequest {
  return { ...(discard.page ? { page: discard.page } : {}), themeFiles: [...new Set(discard.themeFiles)] };
}
