import { isImage as isImageName } from "@vc-shell/framework";
import type { AssetEntry } from "../types";

export function getAssetKey(entry: AssetEntry | undefined): string | undefined {
  return entry?.relativeUrl || entry?.url;
}

export function isImageEntry(entry: AssetEntry | undefined): boolean {
  if (!entry || entry.type !== "blob") {
    return false;
  }

  if (entry.contentType?.startsWith("image/")) {
    return true;
  }

  return isImageName(entry.name) || /\.(avif|bmp|ico|webp)$/i.test(entry.name);
}

export function getEntryIcon(entry: AssetEntry): string {
  // NOTE: `getFileThumbnail` (per-extension icons) was removed in @vc-shell/framework 2.0.3.
  // Fall back to a generic file icon; add an extension→icon map here if per-type icons are needed.
  return entry.type === "folder" ? "lucide-folder" : "lucide-file";
}

export function getReferencesCount(entry: AssetEntry): number {
  return entry.type === "blob" ? entry.referencesCount ?? 0 : 0;
}

export function getFolderUrl(entry: AssetEntry, currentFolderUrl: string): string {
  if (entry.type === "folder") {
    return entry.relativeUrl || entry.url || currentFolderUrl;
  }

  const relativeUrl = entry.relativeUrl || "";
  const suffix = `/${entry.name}`;

  return relativeUrl.endsWith(suffix)
    ? relativeUrl.slice(0, -suffix.length)
    : currentFolderUrl;
}
