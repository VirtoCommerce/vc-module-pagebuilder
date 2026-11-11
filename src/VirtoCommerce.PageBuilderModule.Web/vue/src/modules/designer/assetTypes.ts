import type { Field } from "./types";

export function assetTypes(field: Pick<Field, "type"> & { accept?: unknown }): string[] {
  const accept = typeof field.accept === "string" ? field.accept.split(",").map((type) => type.trim()).filter(Boolean) : [];
  if (accept.length || field.type !== "images") return accept;
  return ["image/*"];
}
