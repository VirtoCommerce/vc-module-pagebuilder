import { toValue, type ComputedRef, type MaybeRefOrGetter, type Ref } from "vue";
import { notification, parseError } from "@vc-shell/framework";
import type { AssetEntry } from "../types";
import { matchesAcceptedTypes } from "../utilities/assetAccept";
import type { DeleteAssetReferences } from "./useAssetReferences";
import {
  prepareAssetUploadFiles,
  type AssetUploadConflict,
  type AssetUploadConflictDecision,
} from "../utilities/assetUpload";

export type { AssetUploadConflict, AssetUploadConflictDecision } from "../utilities/assetUpload";

export interface AssetDeleteConfirmation {
  entry: AssetEntry;
  references: DeleteAssetReferences;
}

interface UseAssetLibraryActionsOptions {
  t: (key: string, params?: Record<string, unknown>) => string;
  canCreate: ComputedRef<boolean>;
  currentFolderUrl: Ref<string>;
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
  /** Shown inside the library, so that it nests in a dialog that hosts the library. */
  requestDeleteConfirmation: (confirmation: AssetDeleteConfirmation) => Promise<boolean>;
  getAssetPublicUrl: (entry: AssetEntry) => string | undefined;
  acceptedTypes?: MaybeRefOrGetter<readonly string[]>;
}

export interface AssetActionResult {
  succeeded: boolean;
  errorMessage?: string;
}

export function useAssetLibraryActions(options: UseAssetLibraryActionsOptions) {
  function getErrorMessage(error: unknown): string {
    const parsed = parseError(error);
    return parsed.message || options.t("ASSET_LIBRARY.NOTIFICATIONS.ERROR_GENERIC");
  }

  function notifyError(error: unknown) {
    notification.error(getErrorMessage(error));
  }

  // The library asks about one name conflict at a time, and a batch checks names against the uploads before it.
  let uploadQueue: Promise<boolean> = Promise.resolve(true);

  function uploadAssets(files: FileList | File[] | undefined, folderUrl?: string): Promise<boolean> {
    if (!options.canCreate.value || !files?.length) {
      return Promise.resolve(false);
    }

    const batch = acceptedFiles(Array.from(files));
    if (!batch.length) {
      return Promise.resolve(false);
    }

    const targetFolderUrl = folderUrl || options.currentFolderUrl.value;
    uploadQueue = uploadQueue.then(() => uploadBatch(batch, targetFolderUrl));
    return uploadQueue;
  }

  function acceptedFiles(files: File[]): File[] {
    const acceptedTypes = toValue(options.acceptedTypes) ?? [];
    const accepted = files.filter((file) => matchesAcceptedTypes({ name: file.name, contentType: file.type }, acceptedTypes));

    if (accepted.length < files.length) {
      notification.warning(
        options.t("ASSET_LIBRARY.NOTIFICATIONS.NOT_ACCEPTED", {
          names: files.filter((file) => !accepted.includes(file)).map((file) => file.name).join(", "),
          types: acceptedTypes.join(", "),
        }),
      );
    }

    return accepted;
  }

  async function uploadBatch(files: File[], targetFolderUrl: string): Promise<boolean> {
    try {
      const preparedFiles = await prepareAssetUploadFiles(files, targetFolderUrl, {
        findAssetByName: options.findAssetByName,
        getReferences: options.getDeleteReferences,
        requestDecision: options.requestUploadConflictDecision,
        getRequiredError: () => options.t("ASSET_LIBRARY.OVERWRITE.VALIDATION.REQUIRED"),
        getInvalidError: () => options.t("ASSET_LIBRARY.OVERWRITE.VALIDATION.INVALID"),
        getCollisionError: (fileName) => options.t("ASSET_LIBRARY.OVERWRITE.VALIDATION.COLLISION", { name: fileName }),
      });

      if (!preparedFiles) {
        notification.warning(options.t("ASSET_LIBRARY.NOTIFICATIONS.UPLOAD_CANCELED"));
        return false;
      }

      await options.uploadFiles(preparedFiles, targetFolderUrl);
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

    const confirmed = await options.requestDeleteConfirmation({ entry, references });

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

  return {
    notifyError,
    uploadAssets,
    createAssetFolder,
    replaceAsset,
    copyAssetUrl,
    confirmDelete,
  };
}
