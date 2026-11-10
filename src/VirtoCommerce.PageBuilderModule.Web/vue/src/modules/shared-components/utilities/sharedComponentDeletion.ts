import type { SharedComponent } from "../types";

interface SharedComponentDeleteOptions {
  allowed: boolean;
  refreshComponent: (component: SharedComponent) => Promise<SharedComponent>;
  confirm: (component: SharedComponent) => Promise<boolean>;
  deleteComponent: (component: SharedComponent) => Promise<void>;
  reload: () => Promise<void>;
  onBlocked: (component: SharedComponent) => void;
  onDeleted: (component: SharedComponent) => void;
  onConflict: (component: SharedComponent) => void;
  onMissing: (component: SharedComponent) => void;
  onError: (error: unknown) => void;
}

export async function deleteSharedComponentWithPreflight(
  component: SharedComponent,
  options: SharedComponentDeleteOptions,
): Promise<boolean> {
  if (!options.allowed) {
    return false;
  }

  let currentComponent: SharedComponent;

  try {
    currentComponent = await options.refreshComponent(component);
  } catch (error) {
    if (isNotFound(error)) {
      await reloadStaleList(options);
      options.onMissing(component);
      return false;
    }

    options.onError(error);
    return false;
  }

  if (currentComponent.usageCount > 0) {
    options.onBlocked(currentComponent);
    return false;
  }

  if (!(await options.confirm(currentComponent))) {
    return false;
  }

  try {
    await options.deleteComponent(currentComponent);
    options.onDeleted(currentComponent);

    try {
      await options.reload();
    } catch {
      // Deletion already succeeded; a failed best-effort refresh must not turn it into an error.
    }

    return true;
  } catch (error) {
    if (isConflict(error)) {
      await reloadStaleList(options);
      options.onConflict(currentComponent);
      return false;
    }

    if (isNotFound(error)) {
      await reloadStaleList(options);
      options.onMissing(currentComponent);
      return false;
    }

    options.onError(error);
    return false;
  }
}

async function reloadStaleList(options: SharedComponentDeleteOptions): Promise<void> {
  try {
    await options.reload();
  } catch {
    // The message about the stale row stays actionable even if refreshing the list also fails.
  }
}

function isConflict(error: unknown): boolean {
  return hasStatus(error, 409);
}

// Another editor deleted the component after this list was loaded.
function isNotFound(error: unknown): boolean {
  return hasStatus(error, 404);
}

function hasStatus(error: unknown, status: number): boolean {
  return typeof error === "object" && error !== null && "status" in error && error.status === status;
}
