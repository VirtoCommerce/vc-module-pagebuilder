import assert from "node:assert/strict";
import test from "node:test";
import { discardChoice, discardOptions } from "../src/modules/designer/discard";
import { PERMISSIONS, partPermission, refusedParts } from "../src/modules/designer/permissions";

const only =
  (...granted: string[]) =>
  (permission: string) =>
    granted.includes(permission);

test("each part is saved and discarded with the permission the server checks for it", () => {
  assert.equal(partPermission("page", false), PERMISSIONS.update);
  assert.equal(partPermission("page", true), PERMISSIONS.templates);
  assert.equal(partPermission("rules", false), PERMISSIONS.templates);
  for (const part of ["header", "footer", "settings", "brand", "palette", "darkPalette"] as const) assert.equal(partPermission(part, false), PERMISSIONS.theme);
});

test("a refused Save names the parts the editor may not save, once each", () => {
  assert.deepEqual(refusedParts(["page", "header", "rules", "header", "settings"], false, only(PERMISSIONS.update)), ["header", "rules", "settings"]);
  assert.deepEqual(refusedParts(["page", "rules"], true, only(PERMISSIONS.update, PERMISSIONS.theme)), ["page", "rules"]);
  // a refusal for a reason the parts don't explain names none
  assert.deepEqual(refusedParts(["page"], false, only(PERMISSIONS.update)), []);
});

test("Discard offers the page's draft ticked and each theme part's unticked; only what the editor may edit can be ticked", () => {
  const options = discardOptions({ page: true, theme: ["rules", "header", "brand"], template: false, hasAccess: only(PERMISSIONS.update, PERMISSIONS.theme) });
  assert.deepEqual(options, [
    { part: "page", checked: true, allowed: true },
    { part: "rules", checked: false, allowed: false },
    { part: "header", checked: false, allowed: true },
    { part: "brand", checked: false, allowed: true },
  ]);
  // an open template is a theme file: builder:templates
  assert.deepEqual(discardOptions({ page: true, theme: [], template: true, hasAccess: only(PERMISSIONS.update) }), [{ part: "page", checked: false, allowed: false }]);
  assert.deepEqual(discardOptions({ page: false, theme: [], template: false, hasAccess: only() }), []);
});

test("Discard sends only the parts ticked that it offered and the editor may discard", () => {
  const options = discardOptions({ page: true, theme: ["rules", "header"], template: false, hasAccess: only(PERMISSIONS.update, PERMISSIONS.theme) });
  assert.deepEqual(discardChoice(options, ["page"]), { page: true, theme: [] });
  assert.deepEqual(discardChoice(options, ["header", "rules", "footer"]), { page: false, theme: ["header"] });
  assert.deepEqual(discardChoice(options, []), { page: false, theme: [] });
});
