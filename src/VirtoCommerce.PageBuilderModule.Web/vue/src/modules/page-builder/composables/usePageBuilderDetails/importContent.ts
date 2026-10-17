/** Adapt legacy Designer exports to the envelope required by atomic page creation. */
export function serializeImportedPageContent(content: unknown): string {
  const document = typeof content === "string" ? JSON.parse(content) : content;
  if (Array.isArray(document)) {
    // The legacy format stores page settings in the first slot, followed by sections.
    const [settings, ...sections] = document;
    return JSON.stringify({ settings: settings || {}, content: sections, version: 1 });
  }

  // Preserve the exact current-format document, including extensions and formatting.
  return typeof content === "string" ? content : JSON.stringify(content);
}
