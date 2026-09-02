import type { ComputedRef } from "vue";
import { notification, parseError, usePopup } from "@vc-shell/framework";
import type { AssetEntry, AssetReferencePage } from "../types";
import type { DeleteAssetReferences } from "./useAssetReferences";
import {
  prepareAssetUploadFiles,
  type AssetUploadConflict,
  type AssetUploadConflictDecision,
} from "../utilities/assetUpload";

export type { AssetUploadConflict, AssetUploadConflictDecision } from "../utilities/assetUpload";

interface UseAssetLibraryActionsOptions {
  t: (key: string, params?: Record<string, unknown>) => string;
  canCreate: ComputedRef<boolean>;
  currentFolderUrl: ComputedRef<string>;
  uploadFiles: (files: FileList | File[], folderUrl?: string) => Promise<void>;
  createFolder: (name: string) => Promise<void>;
  replaceSelectedAsset: (file: File) => Promise<void>;
  deleteEntry: (entry: AssetEntry) => Promise<void>;
  getDeleteReferences: (entry: AssetEntry) => Promise<DeleteAssetReferences>;
  findAssetByName: (folderUrl: string, fileName: string) => Promise<AssetEntry | undefined>;
  requestUploadConflictDecision: (
    conflict: AssetUploadConflict,
    validateName: (fileName: string) => Promise<string | undefined>,
  ) => Promise<AssetUploadConflictDecision>;
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
      const targetFolderUrl = folderUrl || options.currentFolderUrl.value;
      const preparedFiles = await prepareAssetUploadFiles(Array.from(files), targetFolderUrl, {
        findAssetByName: options.findAssetByName,
        getReferences: options.getDeleteReferences,
        requestDecision: options.requestUploadConflictDecision,
        getRequiredError: () => options.t("ASSET_LIBRARY.OVERWRITE.VALIDATION.REQUIRED"),
        getInvalidError: () => options.t("ASSET_LIBRARY.OVERWRITE.VALIDATION.INVALID"),
        getCollisionError: (fileName) => options.t("ASSET_LIBRARY.OVERWRITE.VALIDATION.COLLISION", { name: fileName }),
      });

      if (!preparedFiles) {
        return false;
      }

      await options.uploadFiles(preparedFiles, folderUrl);
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
      const key = referencesCount === 1
        ? "ASSET_LIBRARY.CONFIRM.DELETE_USED_ONE"
        : "ASSET_LIBRARY.CONFIRM.DELETE_USED_MANY";

      const message = options.t(key, {
        name: entry.name,
        count: referencesCount,
      });

      const pageNames = getReferencePageNames(references.referencePages);
      return pageNames.length
        ? `${message}\n\n${pageNames.join("\n")}`
        : message;
    }

    return options.t("ASSET_LIBRARY.CONFIRM.DELETE_SINGLE", { name: entry.name });
  }

  function getReferencePageNames(pages: AssetReferencePage[]): string[] {
    return [...new Set(pages.map((page) => page.name || page.permalink || page.id).filter((name): name is string => !!name))]
      .map((name) => `- ${name}`);
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
