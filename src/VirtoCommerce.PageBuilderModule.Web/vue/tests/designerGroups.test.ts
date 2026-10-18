import assert from "node:assert/strict";
import test from "node:test";
import { groupItems, pageOnly, withGroups } from "../src/modules/designer/groups";
import type { Item, PageDocument } from "../src/modules/designer/types";

const page: PageDocument = { settings: { type: "settings" } as Item, content: [{ id: "p1", type: "text" }] };
const header: Item[] = [{ id: "a1", type: "announcement-bar", blocks: [{ id: "b1", type: "announcement", text: "Hi" }] }];
const footer: Item[] = [{ id: "f1", type: "site-footer" }];

test("the header and footer sit around the page's sections while designing, tagged", () => {
  const doc = withGroups(page, { header, footer });
  assert.deepEqual(doc.content.map((x) => [x.id, x.__group]), [["a1", "header"], ["p1", undefined], ["f1", "footer"]]);
});

test("saving splits them out again, as each file keeps them", () => {
  const doc = withGroups(page, { header, footer });
  assert.deepEqual(pageOnly(doc), page);
  assert.deepEqual(groupItems(doc, "header"), header);
  assert.deepEqual(groupItems(doc, "footer"), footer);
  // a reload replaces the groups, never doubles them
  assert.deepEqual(withGroups(doc, { header: [], footer }).content.map((x) => x.id), ["p1", "f1"]);
});
