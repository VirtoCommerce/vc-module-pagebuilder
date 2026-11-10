import type { Field } from "./types";

/** The asset types a files or images field takes (docs/controls): its comma-separated `accept`, else images for an images field and any type for a files field. */
export function assetTypes(field: Pick<Field, "type"> & { accept?: unknown }): string[] {
  const accept = typeof field.accept === "string" ? field.accept.split(",").map((type) => type.trim()).filter(Boolean) : [];
  if (accept.length || field.type !== "images") return accept;
  return ["image/*"];
}
