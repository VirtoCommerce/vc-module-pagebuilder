import assert from "node:assert/strict";
import test from "node:test";
import {
  getChangedFields,
  parseOverriddenFields,
} from "../src/modules/page-builder/composables/useMultilanguageInheritance";

test("parseOverriddenFields keeps known fields of the backend mask", () => {
  assert.deepEqual(parseOverriddenFields("Permalink, EndDate,Unknown"), ["Permalink", "EndDate"]);
  assert.deepEqual(parseOverriddenFields(""), []);
  assert.deepEqual(parseOverriddenFields(undefined), []);
});

test("getChangedFields reports changed inheritable settings only", () => {
  const before = { permalink: "/faq", visibility: true, userGroups: "vip", startDate: undefined };
  const after = { permalink: "/about", visibility: true, userGroups: "vip", startDate: undefined };

  assert.deepEqual(getChangedFields(before, after), ["Permalink"]);
});

test("getChangedFields treats empty values alike and compares dates by instant", () => {
  const before = { userGroups: "", organizationId: undefined, startDate: "2026-01-01T00:00:00Z" };
  const after = { userGroups: undefined, organizationId: "", startDate: new Date("2026-01-01T00:00:00.000Z") };

  assert.deepEqual(getChangedFields(before, after), []);
});
