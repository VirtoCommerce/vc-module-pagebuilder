import type { ComputedRef } from "vue";
import { notification, parseError, usePopup } from "@vc-shell/framework";
import type { AssetEntry } from "../types";
import { getReferenceSharedComponentNames, getReferencePageNames } from "../utilities/assetReferences";
import type { DeleteAssetReferences } from "./useAssetReferences";

interface UseAssetLibraryActionsOptions {
  t: (key: string, params?: Record<string, unknown>) => string;
  canCreate: ComputedRef<boolean>;
  uploadFiles: (files: FileList | File[], folderUrl?: string) => Promise<void>;
  createFolder: (name: string) => Promise<void>;
  replaceSelectedAsset: (file: File) => Promise<void>;
  deleteEntry: (entry: AssetEntry) => Promise<void>;
  getDeleteReferences: (entry: AssetEntry) => Promise<DeleteAssetReferences>;
  getAssetPublicUrl: (entry: AssetEntry) => string | undefined;
}

export interface AssetActionResult {
  succeeded: boolean;
  errorMessage?: string;
}

export function useAssetLibraryActions(options: UseAssetLibraryActionsOptions) {
  const { showConfirmation } = usePopup();

  function getErrorMessage(error: unknown): string {
    const parsed = parseError(error);
    return parsed.message || options.t("ASSET_LIBRARY.NOTIFICATIONS.ERROR_GENERIC");
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
      notification.success(options.t("ASSET_LIBRARY.NOTIFICATIONS.UPLOADED"));
      return true;
    } catch (error) {
      notifyError(error);
      return false;
    }
  }

  async function createAssetFolder(name: string): Promise<AssetActionResult> {
    const value = name.trim();

    if (!value) {
      return { succeeded: false };
    }

    try {
      await options.createFolder(value);
      notification.success(options.t("ASSET_LIBRARY.NOTIFICATIONS.FOLDER_CREATED"));
      return { succeeded: true };
    } catch (error) {
      return {
        succeeded: false,
        errorMessage: getErrorMessage(error),
      };
    }
  }

  async function replaceAsset(file: File): Promise<void> {
    try {
      await options.replaceSelectedAsset(file);
      notification.success(options.t("ASSET_LIBRARY.NOTIFICATIONS.REPLACED"));
    } catch (error) {
      notifyError(error);
    }
  }

  async function copyAssetUrl(entry: AssetEntry): Promise<void> {
    const value = options.getAssetPublicUrl(entry) || entry.url || entry.relativeUrl;

    if (!value) {
      return;
    }

    if (!navigator.clipboard?.writeText) {
      notification.error(options.t("ASSET_LIBRARY.NOTIFICATIONS.COPY_FAILED"));
      return;
    }

    try {
      await navigator.clipboard.writeText(value);
      notification.success(options.t("ASSET_LIBRARY.NOTIFICATIONS.URL_COPIED"));
    } catch {
      notification.error(options.t("ASSET_LIBRARY.NOTIFICATIONS.COPY_FAILED"));
    }
  }

  async function confirmDelete(entry: AssetEntry): Promise<void> {
    let references: DeleteAssetReferences;

    try {
      references = await options.getDeleteReferences(entry);
    } catch (error) {
      notifyError(error);
      return;
    }

    const confirmed = await showConfirmation(getDeleteConfirmationMessage(entry, references));

    if (!confirmed) {
      return;
    }

    try {
      await options.deleteEntry(entry);
      notification.success(options.t("ASSET_LIBRARY.NOTIFICATIONS.DELETED"));
    } catch (error) {
      notifyError(error);
    }
  }

  function getDeleteConfirmationMessage(entry: AssetEntry, references: DeleteAssetReferences): string {
    const referencesCount = references.referencesCount;

    if (referencesCount > 0) {
      const key =
        referencesCount === 1 ? "ASSET_LIBRARY.CONFIRM.DELETE_USED_ONE" : "ASSET_LIBRARY.CONFIRM.DELETE_USED_MANY";

      const message = options.t(key, {
        name: entry.name,
        count: referencesCount,
      });

      const referenceSections = getReferenceSections(references);
      return referenceSections.length ? `${message}\n\n${referenceSections.join("\n\n")}` : message;
    }

    return options.t("ASSET_LIBRARY.CONFIRM.DELETE_SINGLE", { name: entry.name });
  }

  function getReferenceSections(references: DeleteAssetReferences): string[] {
    const sections: string[] = [];
    const pageNames = getReferencePageNames(references.referencePages);
    const sharedComponentNames = getReferenceSharedComponentNames(references.referenceSharedComponents);

    if (pageNames.length) {
      sections.push(`${options.t("ASSET_LIBRARY.DETAILS.PAGES")}:\n${formatReferenceNames(pageNames)}`);
    }

    if (sharedComponentNames.length) {
      sections.push(
        `${options.t("ASSET_LIBRARY.DETAILS.SHARED_COMPONENTS")}:\n${formatReferenceNames(sharedComponentNames)}`,
      );
    }

    return sections;
  }

  function formatReferenceNames(names: string[]): string {
    return names.map((name) => `- ${name}`).join("\n");
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
