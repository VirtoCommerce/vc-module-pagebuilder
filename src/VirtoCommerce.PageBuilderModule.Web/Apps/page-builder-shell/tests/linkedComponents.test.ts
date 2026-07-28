import assert from "node:assert/strict";
import test from "node:test";
import type { LinkedComponent } from "../src/modules/linked-components/types";
import { deleteLinkedComponentWithPreflight } from "../src/modules/linked-components/utilities/LinkedComponentDelete";
import {
  LinkedComponentDeleteConflictError,
  normalizeLinkedComponentDeleteError,
} from "../src/modules/linked-components/utilities/LinkedComponentDeleteError";
import { createLinkedComponentDetailsLoader } from "../src/modules/linked-components/utilities/LinkedComponentDetailsLoader";
import { getDistinctUsagePages, getUsagePageTitle } from "../src/modules/linked-components/utilities/linkedComponent";

interface Deferred<T> {
  promise: Promise<T>;
  resolve: (value: T) => void;
  reject: (reason?: unknown) => void;
}

function createDeferred<T>(): Deferred<T> {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((promiseResolve, promiseReject) => {
    resolve = promiseResolve;
    reject = promiseReject;
  });

  return { promise, resolve, reject };
}

function createComponent(id: string, usageCount = 0): LinkedComponent {
  return {
    id,
    storeId: "store-1",
    name: `Component ${id}`,
    usageCount,
    usagePages: [],
  };
}

test("delete error adapter normalizes the generated client's 409 response body", () => {
  const conflictBody = createComponent("a", 2);
  const normalized = normalizeLinkedComponentDeleteError(conflictBody, "a");

  assert.ok(normalized instanceof LinkedComponentDeleteConflictError);
  assert.equal(normalized.status, 409);
  assert.strictEqual(normalized.component, conflictBody);
});

test("delete error adapter preserves unrelated errors", () => {
  const serverError = { status: 500, message: "Server error" };
  const otherComponent = createComponent("b", 1);
  const unusedComponent = createComponent("a", 0);
  const arbitraryError = new Error("Network error");

  assert.strictEqual(normalizeLinkedComponentDeleteError(serverError, "a"), serverError);
  assert.strictEqual(normalizeLinkedComponentDeleteError(otherComponent, "a"), otherComponent);
  assert.strictEqual(normalizeLinkedComponentDeleteError(unusedComponent, "a"), unusedComponent);
  assert.strictEqual(normalizeLinkedComponentDeleteError(arbitraryError, "a"), arbitraryError);
});

test("getDistinctUsagePages keeps one entry per grouped page", () => {
  const pages = getDistinctUsagePages([
    { id: "page-1", name: "Home", status: "Draft" },
    { id: "page-1", name: "Home", status: "Published" },
    { id: "page-2", name: "About" },
  ]);

  assert.deepEqual(pages, [
    { id: "page-1", name: "Home", status: "Draft" },
    { id: "page-2", name: "About" },
  ]);
});

test("getDistinctUsagePages falls back to stable page metadata when id is missing", () => {
  const pages = getDistinctUsagePages([
    { permalink: "/home", cultureName: "en-US", status: "Published", name: "Home" },
    { permalink: "/home", cultureName: "en-US", status: "Published", name: "Home" },
    {},
  ]);

  assert.equal(pages.length, 1);
});

test("getUsagePageTitle prefers name, permalink, and id in that order", () => {
  assert.equal(getUsagePageTitle({ id: "page-1", permalink: "/home", name: "Home" }), "Home");
  assert.equal(getUsagePageTitle({ id: "page-1", permalink: "/home" }), "/home");
  assert.equal(getUsagePageTitle({ id: "page-1" }), "page-1");
});

test("details loader ignores a stale reload response after another component is selected", async () => {
  const componentA = createComponent("a");
  const componentB = createComponent("b");
  const requests = new Map<string, Deferred<LinkedComponent>>();
  const applied: string[] = [];
  const loadingStates: boolean[] = [];
  let selectedComponent: LinkedComponent | undefined = componentA;

  const loader = createLinkedComponentDetailsLoader({
    getComponent: (id) => {
      const request = createDeferred<LinkedComponent>();
      requests.set(id, request);
      return request.promise;
    },
    getSelectedComponentId: () => selectedComponent?.id,
    selectComponent: (component) => {
      selectedComponent = component;
    },
    applyComponent: (component) => {
      selectedComponent = component;
      applied.push(component.id);
    },
    clearSelectedComponent: (id) => {
      if (selectedComponent?.id === id) {
        selectedComponent = undefined;
      }
    },
    onLoadingChange: (loading) => loadingStates.push(loading),
  });

  const reloadA = loader.load(componentA);
  const selectB = loader.load(componentB);

  requests.get("b")?.resolve({ ...componentB, name: "Fresh B" });
  await selectB;
  requests.get("a")?.resolve({ ...componentA, name: "Late A" });
  await reloadA;

  assert.equal(selectedComponent?.id, "b");
  assert.equal(selectedComponent?.name, "Fresh B");
  assert.deepEqual(applied, ["b"]);
  assert.deepEqual(loadingStates, [true, true, false]);
});

test("details loader does not apply an outstanding response after dispose", async () => {
  const component = createComponent("a");
  const request = createDeferred<LinkedComponent>();
  let applied = false;
  let selectedComponent: LinkedComponent | undefined;
  const loader = createLinkedComponentDetailsLoader({
    getComponent: () => request.promise,
    getSelectedComponentId: () => selectedComponent?.id,
    selectComponent: (value) => {
      selectedComponent = value;
    },
    applyComponent: () => {
      applied = true;
    },
    clearSelectedComponent: () => {
      selectedComponent = undefined;
    },
    onLoadingChange: () => undefined,
  });

  const pendingLoad = loader.load(component);
  loader.dispose();
  request.resolve({ ...component, name: "Late response" });
  await pendingLoad;

  assert.equal(applied, false);
});

test("details loader suppresses an obsolete request error after another component is selected", async () => {
  const componentA = createComponent("a");
  const componentB = createComponent("b");
  const requestA = createDeferred<LinkedComponent>();
  const requestB = createDeferred<LinkedComponent>();
  let selectedComponent: LinkedComponent | undefined;
  const loader = createLinkedComponentDetailsLoader({
    getComponent: (id) => (id === "a" ? requestA.promise : requestB.promise),
    getSelectedComponentId: () => selectedComponent?.id,
    selectComponent: (component) => {
      selectedComponent = component;
    },
    applyComponent: (component) => {
      selectedComponent = component;
    },
    clearSelectedComponent: () => {
      selectedComponent = undefined;
    },
    onLoadingChange: () => undefined,
  });

  const loadA = loader.load(componentA);
  const loadB = loader.load(componentB);
  requestB.resolve({ ...componentB, name: "Fresh B" });
  await loadB;
  requestA.reject(new Error("Late A failure"));

  await assert.doesNotReject(loadA);
  assert.equal(selectedComponent?.id, "b");
  assert.equal(selectedComponent?.name, "Fresh B");
});

test("delete preflight blocks deletion using fresh usage and emits one blocked notification", async () => {
  const staleComponent = createComponent("a", 0);
  const freshComponent = createComponent("a", 3);
  let confirmations = 0;
  let deletes = 0;
  let blockedNotifications = 0;

  const deleted = await deleteLinkedComponentWithPreflight(staleComponent, {
    allowed: true,
    refreshComponent: async () => freshComponent,
    confirm: async () => {
      confirmations++;
      return true;
    },
    deleteComponent: async () => {
      deletes++;
    },
    reload: async () => undefined,
    onBlocked: () => {
      blockedNotifications++;
    },
    onDeleted: () => undefined,
    onConflict: () => undefined,
    onError: () => undefined,
  });

  assert.equal(deleted, false);
  assert.equal(confirmations, 0);
  assert.equal(deletes, 0);
  assert.equal(blockedNotifications, 1);
});

test("delete conflict reloads stale state and emits only the conflict notification", async () => {
  const component = createComponent("a");
  let reloads = 0;
  let conflictNotifications = 0;
  let errorNotifications = 0;
  let successNotifications = 0;

  const deleted = await deleteLinkedComponentWithPreflight(component, {
    allowed: true,
    refreshComponent: async () => component,
    confirm: async () => true,
    deleteComponent: async () => {
      throw normalizeLinkedComponentDeleteError(createComponent("a", 1), "a");
    },
    reload: async () => {
      reloads++;
    },
    onBlocked: () => undefined,
    onDeleted: () => {
      successNotifications++;
    },
    onConflict: () => {
      conflictNotifications++;
    },
    onError: () => {
      errorNotifications++;
    },
  });

  assert.equal(deleted, false);
  assert.equal(reloads, 1);
  assert.equal(conflictNotifications, 1);
  assert.equal(errorNotifications, 0);
  assert.equal(successNotifications, 0);
});

test("delete conflict still emits only the conflict notification when its reload fails", async () => {
  const component = createComponent("a");
  let conflictNotifications = 0;
  let errorNotifications = 0;

  const deleted = await deleteLinkedComponentWithPreflight(component, {
    allowed: true,
    refreshComponent: async () => component,
    confirm: async () => true,
    deleteComponent: async () => {
      throw normalizeLinkedComponentDeleteError(createComponent("a", 1), "a");
    },
    reload: async () => {
      throw new Error("Reload failed");
    },
    onBlocked: () => undefined,
    onDeleted: () => undefined,
    onConflict: () => {
      conflictNotifications++;
    },
    onError: () => {
      errorNotifications++;
    },
  });

  assert.equal(deleted, false);
  assert.equal(conflictNotifications, 1);
  assert.equal(errorNotifications, 0);
});

test("successful delete stays successful and notifies once when its best-effort reload fails", async () => {
  const component = createComponent("a");
  let reloads = 0;
  let successNotifications = 0;
  let errorNotifications = 0;

  const deleted = await deleteLinkedComponentWithPreflight(component, {
    allowed: true,
    refreshComponent: async () => component,
    confirm: async () => true,
    deleteComponent: async () => undefined,
    reload: async () => {
      reloads++;
      throw new Error("Reload failed after delete");
    },
    onBlocked: () => undefined,
    onDeleted: () => {
      successNotifications++;
    },
    onConflict: () => undefined,
    onError: () => {
      errorNotifications++;
    },
  });

  assert.equal(deleted, true);
  assert.equal(reloads, 1);
  assert.equal(successNotifications, 1);
  assert.equal(errorNotifications, 0);
});
