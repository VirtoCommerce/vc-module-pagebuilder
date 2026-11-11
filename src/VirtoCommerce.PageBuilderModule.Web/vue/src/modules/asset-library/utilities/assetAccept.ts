/**
 * Applies the accepted types as the server's asset search does.
 * A file the browser gives no type passes the type entries: the server reads its type from the name.
 */
export function matchesAcceptedTypes(asset: { name: string; contentType?: string }, acceptedTypes: readonly string[]): boolean {
  const types = acceptedTypes.map((type) => type.trim().toLowerCase()).filter(Boolean);
  return !types.length || types.some((type) => matchesAcceptedType(asset.name.toLowerCase(), (asset.contentType ?? "").toLowerCase(), type));
}

function matchesAcceptedType(name: string, contentType: string, type: string): boolean {
  if (type.startsWith(".")) {
    return name.endsWith(type);
  }

  if (!contentType) {
    return true;
  }

  if (type.endsWith("/*")) {
    return contentType.startsWith(type.slice(0, -1));
  }

  return contentType === type;
}
