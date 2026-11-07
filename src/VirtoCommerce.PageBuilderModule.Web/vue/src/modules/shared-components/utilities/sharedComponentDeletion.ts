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
      try {
        await options.reload();
      } catch {
        // The conflict remains actionable even if refreshing the stale view also fails.
      }

      options.onConflict(currentComponent);
      return false;
    }

    options.onError(error);
    return false;
  }
}

function isConflict(error: unknown): boolean {
  return typeof error === "object" && error !== null && "status" in error && error.status === 409;
}
