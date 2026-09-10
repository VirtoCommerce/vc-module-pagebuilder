import assert from "node:assert/strict";
import test from "node:test";
import type { SharedComponent } from "../src/modules/shared-components/types";
import { deleteSharedComponentWithPreflight } from "../src/modules/shared-components/utilities/sharedComponentDeletion";
import {
  SharedComponentDeleteConflictError,
  normalizeSharedComponentDeleteError,
} from "../src/modules/shared-components/utilities/sharedComponentDeletionError";
import { createSharedComponentDetailsLoader } from "../src/modules/shared-components/utilities/sharedComponentDetailsRequest";
import { getDistinctUsagePages, getUsagePageTitle } from "../src/modules/shared-components/utilities/sharedComponent";
import { buildPageDesignerUrl, canOpenPageDesigner } from "../src/utilities/pageDesigner";

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

function createComponent(id: string, usageCount = 0): SharedComponent {
  return {
    id,
    storeId: "store-1",
    name: `Component ${id}`,
    usageCount,
    usagePages: [],
  };
}

test("page designer URL matches the Open designer action and includes the page culture", () => {
  const url = buildPageDesignerUrl(
    {
      groupId: "home page",
      storeId: "B2B/store",
      cultureName: "en-US",
    },
    "https://platform.example.com/",
  );

  assert.equal(
    url,
    "https://platform.example.com/Modules/$(VirtoCommerce.PageBuilderModule)/Content/page-builder-designer/index.html?storeId=B2B%2Fstore#/pages?type=pages&groupId=home+page&cultureName=en-US",
  );
});

test("page designer URL omits an empty culture and requires page identity", () => {
  assert.equal(
    buildPageDesignerUrl({ groupId: "page-1", storeId: "store-1" }, "https://platform.example.com"),
    "https://platform.example.com/Modules/$(VirtoCommerce.PageBuilderModule)/Content/page-builder-designer/index.html?storeId=store-1#/pages?type=pages&groupId=page-1",
  );
  assert.throws(() => buildPageDesignerUrl({ storeId: "store-1" }, "https://platform.example.com"), /Can't open page/);
  assert.equal(canOpenPageDesigner({ groupId: "  ", storeId: "store-1" }), false);
  assert.equal(canOpenPageDesigner({ groupId: "page-1", storeId: "  " }), false);
});

test("page designer URL trims page and store identifiers", () => {
  assert.equal(
    buildPageDesignerUrl({ groupId: " page-1 ", storeId: " store-1 " }, "https://platform.example.com"),
    "https://platform.example.com/Modules/$(VirtoCommerce.PageBuilderModule)/Content/page-builder-designer/index.html?storeId=store-1#/pages?type=pages&groupId=page-1",
  );
});

test("page designer is unavailable for archived pages", () => {
  const context = { groupId: "page-1", storeId: "store-1", status: "Archived" };

  assert.equal(canOpenPageDesigner(context), false);
  assert.throws(() => buildPageDesignerUrl(context, "https://platform.example.com"), /Can't open page/);
});

test("delete error adapter normalizes the generated client's 409 response body", () => {
  const conflictBody = createComponent("a", 2);
  const normalized = normalizeSharedComponentDeleteError(conflictBody, "a");

  assert.ok(normalized instanceof SharedComponentDeleteConflictError);
  assert.equal(normalized.status, 409);
  assert.strictEqual(normalized.component, conflictBody);
});

test("delete error adapter preserves unrelated errors", () => {
  const serverError = { status: 500, message: "Server error" };
  const otherComponent = createComponent("b", 1);
  const unusedComponent = createComponent("a", 0);
  const arbitraryError = new Error("Network error");

  assert.strictEqual(normalizeSharedComponentDeleteError(serverError, "a"), serverError);
  assert.strictEqual(normalizeSharedComponentDeleteError(otherComponent, "a"), otherComponent);
  assert.strictEqual(normalizeSharedComponentDeleteError(unusedComponent, "a"), unusedComponent);
  assert.strictEqual(normalizeSharedComponentDeleteError(arbitraryError, "a"), arbitraryError);
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
  const requests = new Map<string, Deferred<SharedComponent>>();
  const applied: string[] = [];
  const loadingStates: boolean[] = [];
  let selectedComponent: SharedComponent | undefined = componentA;

  const loader = createSharedComponentDetailsLoader({
    getComponent: (id) => {
      const request = createDeferred<SharedComponent>();
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
  const request = createDeferred<SharedComponent>();
  let applied = false;
  let selectedComponent: SharedComponent | undefined;
  const loader = createSharedComponentDetailsLoader({
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

test("details loader invalidation prevents a stale response from overwriting a rename", async () => {
  const component = createComponent("a");
  const request = createDeferred<SharedComponent>();
  let selectedComponent: SharedComponent | undefined;
  const applied: string[] = [];
  const loader = createSharedComponentDetailsLoader({
    getComponent: () => request.promise,
    getSelectedComponentId: () => selectedComponent?.id,
    selectComponent: (value) => {
      selectedComponent = value;
    },
    applyComponent: (value) => {
      selectedComponent = value;
      applied.push(value.name);
    },
    clearSelectedComponent: () => {
      selectedComponent = undefined;
    },
    onLoadingChange: () => undefined,
  });

  const pendingLoad = loader.load(component);
  loader.invalidate();
  selectedComponent = { ...component, name: "Renamed component" };
  request.resolve({ ...component, name: "Stale component name" });
  await pendingLoad;

  assert.equal(selectedComponent?.name, "Renamed component");
  assert.deepEqual(applied, []);
});

test("details loader suppresses an obsolete request error after another component is selected", async () => {
  const componentA = createComponent("a");
  const componentB = createComponent("b");
  const requestA = createDeferred<SharedComponent>();
  const requestB = createDeferred<SharedComponent>();
  let selectedComponent: SharedComponent | undefined;
  const loader = createSharedComponentDetailsLoader({
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

test("delete permission gate stops before refreshing or deleting", async () => {
  const component = createComponent("a");
  let refreshes = 0;
  let confirmations = 0;
  let deletes = 0;

  const deleted = await deleteSharedComponentWithPreflight(component, {
    allowed: false,
    refreshComponent: async () => {
      refreshes++;
      return component;
    },
    confirm: async () => {
      confirmations++;
      return true;
    },
    deleteComponent: async () => {
      deletes++;
    },
    reload: async () => undefined,
    onBlocked: () => undefined,
    onDeleted: () => undefined,
    onConflict: () => undefined,
    onError: () => undefined,
  });

  assert.equal(deleted, false);
  assert.equal(refreshes, 0);
  assert.equal(confirmations, 0);
  assert.equal(deletes, 0);
});

test("delete preflight stops when fresh usage cannot be loaded", async () => {
  const component = createComponent("a");
  const refreshError = new Error("Refresh failed");
  let confirmations = 0;
  let deletes = 0;
  let receivedError: unknown;

  const deleted = await deleteSharedComponentWithPreflight(component, {
    allowed: true,
    refreshComponent: async () => {
      throw refreshError;
    },
    confirm: async () => {
      confirmations++;
      return true;
    },
    deleteComponent: async () => {
      deletes++;
    },
    reload: async () => undefined,
    onBlocked: () => undefined,
    onDeleted: () => undefined,
    onConflict: () => undefined,
    onError: (error) => {
      receivedError = error;
    },
  });

  assert.equal(deleted, false);
  assert.equal(confirmations, 0);
  assert.equal(deletes, 0);
  assert.equal(receivedError, refreshError);
});

test("delete preflight blocks deletion using fresh usage and emits one blocked notification", async () => {
  const staleComponent = createComponent("a", 0);
  const freshComponent = createComponent("a", 3);
  let confirmations = 0;
  let deletes = 0;
  let blockedNotifications = 0;

  const deleted = await deleteSharedComponentWithPreflight(staleComponent, {
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

  const deleted = await deleteSharedComponentWithPreflight(component, {
    allowed: true,
    refreshComponent: async () => component,
    confirm: async () => true,
    deleteComponent: async () => {
      throw normalizeSharedComponentDeleteError(createComponent("a", 1), "a");
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

  const deleted = await deleteSharedComponentWithPreflight(component, {
    allowed: true,
    refreshComponent: async () => component,
    confirm: async () => true,
    deleteComponent: async () => {
      throw normalizeSharedComponentDeleteError(createComponent("a", 1), "a");
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

  const deleted = await deleteSharedComponentWithPreflight(component, {
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
