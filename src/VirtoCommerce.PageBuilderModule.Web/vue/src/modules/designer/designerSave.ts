import type { DesignerDiscardRequest, DesignerSaveRequest } from "../../api_client/virtocommerce.pagebuildermodule";
import type { ThemePart } from "./history";

export type DesignerSavePage = { groupId: string; eTag: string; content: unknown } | { path: string; type: string; content: unknown };

export interface DesignerSaveFile {
  path: string;
  eTag: string;
  content: unknown;
}

export interface PendingThemeFile {
  part: ThemePart;
  file: DesignerSaveFile;
  saved: (eTag?: string) => void;
}

export class CatalogPendingError extends Error {
  constructor() {
    super("Theme files are saved with the catalog of the storefront previewed, which has not loaded yet.");
  }
}

/** The server checks theme files against the catalog named here. */
export function designerSaveUrl(storeId: string, catalogHash?: string) {
  const query = new URLSearchParams({ storeId });
  if (catalogHash) query.set("catalogHash", catalogHash);
  return `/api/pagebuilder/designer/save?${query}`;
}

// Sent as text because the server saves the bytes as they are and the history identifies the published version by them.
const text = (content: unknown) => JSON.stringify(content, null, 2);

export function designerSaveBody(save: { page?: DesignerSavePage; themeFiles: DesignerSaveFile[] }): DesignerSaveRequest {
  return {
    page: save.page && { ...save.page, content: text(save.page.content) },
    themeFiles: save.themeFiles.map((x) => ({ path: x.path, eTag: x.eTag, content: text(x.content) })),
  };
}

export type DesignerDiscardPage = { groupId: string } | { path: string; type: string };

export const designerDiscardUrl = (storeId: string) => `/api/pagebuilder/designer/discard?${new URLSearchParams({ storeId })}`;

export function designerDiscardBody(discard: { page?: DesignerDiscardPage; themeFiles: string[] }): DesignerDiscardRequest {
  return { ...(discard.page ? { page: discard.page } : {}), themeFiles: [...new Set(discard.themeFiles)] };
}
