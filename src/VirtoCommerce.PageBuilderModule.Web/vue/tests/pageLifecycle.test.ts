import assert from "node:assert/strict";
import test from "node:test";
import { pageLifecycle } from "../src/modules/page-builder/pageLifecycle";

test("pageLifecycle follows the list's lifecycle filters", () => {
  const now = new Date("2026-10-15T12:00:00Z");
  const past = new Date("2026-10-01T00:00:00Z");
  const future = new Date("2026-11-01T00:00:00Z");

  assert.equal(pageLifecycle({ status: "Draft" }, now), "Draft");
  assert.equal(pageLifecycle({ status: "Published" }, now), "Active");
  assert.equal(pageLifecycle({ status: "Published", startDate: past, endDate: future }, now), "Active");
  assert.equal(pageLifecycle({ status: "Published", startDate: future }, now), "Pending");
  assert.equal(pageLifecycle({ status: "Published", endDate: past }, now), "Archived");
  assert.equal(pageLifecycle({ status: "Archived" }, now), "Archived");
});
