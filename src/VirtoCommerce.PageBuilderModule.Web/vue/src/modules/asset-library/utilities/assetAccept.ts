/** The accepted types the server's asset search applies: a `.ext` entry matches the name, `type/*` the content type's prefix, any other the exact content type. */
export function matchesAcceptedTypes(asset: { name: string; contentType?: string }, acceptedTypes: readonly string[]): boolean {
  const types = acceptedTypes.map((type) => type.trim().toLowerCase()).filter(Boolean);
  return !types.length || types.some((type) => matchesAcceptedType(asset.name.toLowerCase(), (asset.contentType ?? "").toLowerCase(), type));
}

function matchesAcceptedType(name: string, contentType: string, type: string): boolean {
  if (type.startsWith(".")) {
    return name.endsWith(type);
  }

  if (type.endsWith("/*")) {
    return contentType.startsWith(type.slice(0, -1));
  }

  return contentType === type;
}
