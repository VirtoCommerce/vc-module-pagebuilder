import type { Item } from "./types";

/** The Angular designer's clipboard format. */
export interface ClipboardItem {
  type: "section" | "block";
  content: Item;
}

export const toClipboard = (type: ClipboardItem["type"], content: Item) => JSON.stringify({ content, type }, null, 2);

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
