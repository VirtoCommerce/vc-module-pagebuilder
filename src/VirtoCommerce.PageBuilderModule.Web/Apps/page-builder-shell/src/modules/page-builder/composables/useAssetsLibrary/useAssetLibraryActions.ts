import type { ComputedRef } from "vue";
import { notification, usePopup } from "@vc-shell/framework";
import type { AssetEntry } from "../useAssetsLibraryApi";

interface UseAssetLibraryActionsOptions {
  t: (key: string, params?: Record<string, unknown>) => string;
  canCreate: ComputedRef<boolean>;
  uploadFiles: (files: FileList | File[], folderUrl?: string) => Promise<void>;
  createFolder: (name: string) => Promise<void>;
  replaceSelectedAsset: (file: File) => Promise<void>;
  deleteEntry: (entry: AssetEntry) => Promise<void>;
  getDeleteReferencesCount: (entry: AssetEntry) => Promise<number>;
  getAssetPublicUrl: (entry: AssetEntry) => string | undefined;
}

export function useAssetLibraryActions(options: UseAssetLibraryActionsOptions) {
  const { showConfirmation } = usePopup();

  function getErrorMessage(error: unknown): string {
    return error instanceof Error ? error.message : options.t("PAGE_BUILDER.ASSETS.NOTIFICATIONS.ERROR_GENERIC");
  }

  function notifyError(error: unknown) {
    notification.error(getErrorMessage(error));
  }

  async function uploadAssets(files: FileList | File[] | undefined, folderUrl?: string): Promise<boolean> {
    if (!options.canCreate.value || !files?.length) {
      return false;
    }

    try {
      await options.uploadFiles(files, folderUrl);
      notification.success(options.t("PAGE_BUILDER.ASSETS.NOTIFICATIONS.UPLOADED"));
      return true;
    } catch (error) {
      notifyError(error);
      return false;
    }
  }

  async function createAssetFolder(name: string): Promise<boolean> {
    const value = name.trim();

    if (!value) {
      return false;
    }

    try {
      await options.createFolder(value);
      notification.success(options.t("PAGE_BUILDER.ASSETS.NOTIFICATIONS.FOLDER_CREATED"));
      return true;
    } catch (error) {
      notifyError(error);
      return false;
    }
  }

  async function replaceAsset(file: File): Promise<void> {
    try {
      await options.replaceSelectedAsset(file);
      notification.success(options.t("PAGE_BUILDER.ASSETS.NOTIFICATIONS.REPLACED"));
    } catch (error) {
      notifyError(error);
    }
  }

  async function copyAssetUrl(entry: AssetEntry): Promise<void> {
    const value = options.getAssetPublicUrl(entry) || entry.url || entry.relativeUrl;

    if (!value) {
      return;
    }

    try {
      await navigator.clipboard.writeText(value);
    } catch {
      window.prompt(options.t("PAGE_BUILDER.ASSETS.DETAILS.URL"), value);
    }

    notification.success(options.t("PAGE_BUILDER.ASSETS.NOTIFICATIONS.URL_COPIED"));
  }

  async function confirmDelete(entry: AssetEntry): Promise<void> {
    let referencesCount = 0;

    try {
      referencesCount = await options.getDeleteReferencesCount(entry);
    } catch (error) {
      notifyError(error);
      return;
    }

    const confirmed = await showConfirmation(getDeleteConfirmationMessage(entry, referencesCount));

    if (!confirmed) {
      return;
    }

    try {
      await options.deleteEntry(entry);
      notification.success(options.t("PAGE_BUILDER.ASSETS.NOTIFICATIONS.DELETED"));
    } catch (error) {
      notifyError(error);
    }
  }

  function getDeleteConfirmationMessage(entry: AssetEntry, referencesCount: number): string {
    if (referencesCount > 0) {
      const key = referencesCount === 1
        ? "PAGE_BUILDER.ASSETS.CONFIRM.DELETE_USED_ONE"
        : "PAGE_BUILDER.ASSETS.CONFIRM.DELETE_USED_MANY";

      return options.t(key, {
        name: entry.name,
        count: referencesCount,
      });
    }

    return options.t("PAGE_BUILDER.ASSETS.CONFIRM.DELETE_SINGLE", { name: entry.name });
  }

  return {
    notifyError,
    uploadAssets,
    createAssetFolder,
    replaceAsset,
    copyAssetUrl,
    confirmDelete,
  };
}
