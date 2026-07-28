import type { LinkedComponent } from "../types";

interface LinkedComponentDeleteOptions {
  allowed: boolean;
  refreshComponent: (component: LinkedComponent) => Promise<LinkedComponent>;
  confirm: (component: LinkedComponent) => Promise<boolean>;
  deleteComponent: (component: LinkedComponent) => Promise<void>;
  reload: () => Promise<void>;
  onBlocked: (component: LinkedComponent) => void;
  onDeleted: (component: LinkedComponent) => void;
  onConflict: (component: LinkedComponent) => void;
  onError: (error: unknown) => void;
}

export async function deleteLinkedComponentWithPreflight(
  component: LinkedComponent,
  options: LinkedComponentDeleteOptions,
): Promise<boolean> {
  if (!options.allowed) {
    return false;
  }

  let currentComponent: LinkedComponent;

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
      // The delete already succeeded. Keep the success result and the locally
      // updated state even when the best-effort server refresh fails.
    }

    return true;
  } catch (error) {
    if (isConflict(error)) {
      // The server is the final authority. Refresh the stale list/details before
      // reporting the conflict, but keep this flow to one user-facing message.
      try {
        await options.reload();
      } catch {
        // The conflict remains the actionable result even if refresh fails.
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
