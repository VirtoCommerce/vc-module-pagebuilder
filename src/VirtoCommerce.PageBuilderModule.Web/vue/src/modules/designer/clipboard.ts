import type { Item } from "./types";

/** What Copy puts on the clipboard (the Angular designer's format): { "content": item, "type": "section" | "block" } */
export interface ClipboardItem {
  type: "section" | "block";
  content: Item;
}

export const toClipboard = (type: ClipboardItem["type"], content: Item) => JSON.stringify({ content, type }, null, 2);

/** The copied section or block in a clipboard text, or undefined when it isn't one. */
export function fromClipboard(text: string | undefined): ClipboardItem | undefined {
  try {
    const value = JSON.parse(text ?? "") as Partial<ClipboardItem>;
    const content = value?.content;
    if ((value.type !== "section" && value.type !== "block") || !content || typeof content !== "object") return undefined;
    if (typeof content.type !== "string" || !content.type.trim()) return undefined;
    return { type: value.type, content: content as Item };
  } catch {
    return undefined;
  }
}
