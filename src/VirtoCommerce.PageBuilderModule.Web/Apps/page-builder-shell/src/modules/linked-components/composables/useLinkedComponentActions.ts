import type { ComputedRef } from "vue";
import { useI18n } from "vue-i18n";
import { notification, parseError, usePopup } from "@vc-shell/framework";
import type { LinkedComponent, LinkedComponentActionResult } from "../types";
import { deleteLinkedComponentWithPreflight } from "../utilities";

interface UseLinkedComponentActionsOptions {
  canUpdate: ComputedRef<boolean>;
  canDelete: ComputedRef<boolean>;
  refreshComponent: (component: LinkedComponent) => Promise<LinkedComponent>;
  renameComponent: (component: LinkedComponent, name: string) => Promise<void>;
  deleteComponent: (component: LinkedComponent) => Promise<void>;
  reload: () => Promise<void>;
}

export function useLinkedComponentActions(options: UseLinkedComponentActionsOptions) {
  const { showConfirmation } = usePopup();
  const { t } = useI18n({ useScope: "global" });
  let deleteInProgress = false;

  function getErrorMessage(error: unknown): string {
    const parsed = parseError(error);
    return parsed.message || t("LINKED_COMPONENTS.NOTIFICATIONS.ERROR_GENERIC");
  }

  function notifyError(error: unknown) {
    notification.error(getErrorMessage(error));
  }

  async function rename(component: LinkedComponent, name: string): Promise<LinkedComponentActionResult> {
    const value = name.trim();

    if (!options.canUpdate.value || !value) {
      return { succeeded: false };
    }

    try {
      await options.renameComponent(component, value);
      notification.success(t("LINKED_COMPONENTS.NOTIFICATIONS.RENAMED"));
      return { succeeded: true };
    } catch (error) {
      return {
        succeeded: false,
        errorMessage: getErrorMessage(error),
      };
    }
  }

  async function confirmDelete(component: LinkedComponent): Promise<boolean> {
    if (deleteInProgress) {
      return false;
    }

    deleteInProgress = true;

    try {
      return await deleteLinkedComponentWithPreflight(component, {
        allowed: options.canDelete.value,
        refreshComponent: options.refreshComponent,
        confirm: (currentComponent) =>
          showConfirmation(t("LINKED_COMPONENTS.CONFIRM.DELETE", { name: currentComponent.name })),
        deleteComponent: options.deleteComponent,
        reload: options.reload,
        onBlocked: (currentComponent) => {
          const key =
            currentComponent.usageCount === 1
              ? "LINKED_COMPONENTS.NOTIFICATIONS.DELETE_BLOCKED_ONE"
              : "LINKED_COMPONENTS.NOTIFICATIONS.DELETE_BLOCKED_MANY";
          notification.warning(t(key, { count: currentComponent.usageCount }));
        },
        onDeleted: () => {
          notification.success(t("LINKED_COMPONENTS.NOTIFICATIONS.DELETED"));
        },
        onConflict: () => {
          notification.warning(t("LINKED_COMPONENTS.NOTIFICATIONS.DELETE_CONFLICT"));
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
