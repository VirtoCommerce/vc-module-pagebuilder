import type { AssetEntry } from "../types";

export function safeDecode(value: string): string {
  try {
    return decodeURIComponent(value);
  } catch {
    return value;
  }
}

export function formatAssetDate(value?: string): string | undefined {
  if (!value) {
    return undefined;
  }

  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return undefined;
  }

  return new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium",
  }).format(date);
}

export function toPublicAssetUrl(value: string, origin = getWindowOrigin()): string {
  const trimmed = value.trim();

  if (!trimmed) {
    return "";
  }

  if (trimmed.startsWith("data:")) {
    return trimmed;
  }

  if (isAbsoluteUrl(trimmed)) {
    try {
      return new URL(trimmed, origin).toString();
    } catch {
      return trimmed;
    }
  }

  const assetPath = normalizeAssetPath(trimmed);
  return new URL(assetPath, origin).toString();
}

export function getAssetStoredUrl(entry: AssetEntry): string {
  const value = (entry.url || entry.relativeUrl || "").trim();
  if (!value || value.startsWith("data:") || isAbsoluteUrl(value)) return value;
  return normalizeAssetPath(value);
}

export function getAssetPublicUrl(entry: AssetEntry): string | undefined {
  const assetUrl = entry.url?.trim();

  if (assetUrl) {
    return toPublicAssetUrl(assetUrl);
  }

  return entry.relativeUrl ? toPublicAssetUrl(entry.relativeUrl) : undefined;
}

export function getPreviewUrl(entry: AssetEntry | undefined): string | undefined {
  if (!entry) {
    return undefined;
  }

  const publicUrl = getAssetPublicUrl(entry);

  if (!publicUrl) {
    return undefined;
  }

  if (!entry.modifiedDate) {
    return publicUrl;
  }

  const separator = publicUrl.includes("?") ? "&" : "?";
  return `${publicUrl}${separator}t=${encodeURIComponent(entry.modifiedDate)}`;
}

function isAbsoluteUrl(value: string): boolean {
  return /^(?:[a-z][a-z\d+\-.]*:)?\/\//i.test(value);
}

function ensureLeadingSlash(value: string): string {
  return value.startsWith("/") ? value : `/${value}`;
}

function normalizeAssetPath(value: string): string {
  const path = ensureLeadingSlash(value);

  return path.toLowerCase().startsWith("/stores/") ? `/assets${path}` : path;
}

function getWindowOrigin(): string {
  return globalThis.window?.location?.origin ?? "http://localhost";
}
