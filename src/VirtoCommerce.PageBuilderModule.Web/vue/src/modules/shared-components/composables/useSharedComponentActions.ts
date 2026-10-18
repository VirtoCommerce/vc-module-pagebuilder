import type { ComputedRef } from "vue";
import { useI18n } from "vue-i18n";
import { notification, parseError, usePopup } from "@vc-shell/framework";
import type { SharedComponent, SharedComponentActionResult } from "../types";
import { deleteSharedComponentWithPreflight } from "../utilities";

interface UseSharedComponentActionsOptions {
  canUpdate: ComputedRef<boolean>;
  canDelete: ComputedRef<boolean>;
  refreshComponent: (component: SharedComponent) => Promise<SharedComponent>;
  renameComponent: (component: SharedComponent, name: string) => Promise<void>;
  deleteComponent: (component: SharedComponent) => Promise<void>;
  reload: () => Promise<void>;
}

export function useSharedComponentActions(options: UseSharedComponentActionsOptions) {
  const { showConfirmation } = usePopup();
  const { t } = useI18n({ useScope: "global" });
  let deleteInProgress = false;

  function getErrorMessage(error: unknown): string {
    const parsed = parseError(error);
    return parsed.message || t("SHARED_COMPONENTS.NOTIFICATIONS.ERROR_GENERIC");
  }

  function notifyError(error: unknown) {
    notification.error(getErrorMessage(error));
  }

  async function rename(component: SharedComponent, name: string): Promise<SharedComponentActionResult> {
    const value = name.trim();

    if (!options.canUpdate.value || !value) {
      return { succeeded: false };
    }

    try {
      await options.renameComponent(component, value);
      notification.success(t("SHARED_COMPONENTS.NOTIFICATIONS.RENAMED"));
      void options.reload().catch(() => undefined);
      return { succeeded: true };
    } catch (error) {
      return {
        succeeded: false,
        errorMessage: getErrorMessage(error),
      };
    }
  }

  async function confirmDelete(component: SharedComponent): Promise<boolean> {
    if (deleteInProgress) {
      return false;
    }

    deleteInProgress = true;

    try {
      return await deleteSharedComponentWithPreflight(component, {
        allowed: options.canDelete.value,
        refreshComponent: options.refreshComponent,
        confirm: (currentComponent) =>
          showConfirmation(t("SHARED_COMPONENTS.CONFIRM.DELETE", { name: currentComponent.name })),
        deleteComponent: options.deleteComponent,
        reload: options.reload,
        onBlocked: (currentComponent) => {
          const key =
            currentComponent.usageCount === 1
              ? "SHARED_COMPONENTS.NOTIFICATIONS.DELETE_BLOCKED_ONE"
              : "SHARED_COMPONENTS.NOTIFICATIONS.DELETE_BLOCKED_MANY";
          notification.warning(t(key, { count: currentComponent.usageCount }));
        },
        onDeleted: () => {
          notification.success(t("SHARED_COMPONENTS.NOTIFICATIONS.DELETED"));
        },
        onConflict: () => {
          notification.warning(t("SHARED_COMPONENTS.NOTIFICATIONS.DELETE_CONFLICT"));
        },
        onError: notifyError,
      });
    } finally {
      deleteInProgress = false;
    }
  }

  return {
    notifyError,
    rename,
    confirmDelete,
  };
}
