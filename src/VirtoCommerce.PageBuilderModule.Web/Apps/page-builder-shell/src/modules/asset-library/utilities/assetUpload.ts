import type { AssetEntry } from "../types";
import type { DeleteAssetReferences } from "../composables/useAssetReferences";

export interface AssetUploadConflict {
  file: File;
  existingEntry: AssetEntry;
  references: DeleteAssetReferences;
  source: "stored" | "batch";
}

export type AssetOverwriteMessageKey =
  | "ASSET_LIBRARY.OVERWRITE.MESSAGE_UNUSED"
  | "ASSET_LIBRARY.OVERWRITE.MESSAGE_USED_ONE"
  | "ASSET_LIBRARY.OVERWRITE.MESSAGE_USED_MANY"
  | "ASSET_LIBRARY.OVERWRITE.MESSAGE_BATCH_DUPLICATE";

export type AssetUploadConflictDecision =
  | { action: "replace" }
  | { action: "upload-as"; fileName: string }
  | { action: "cancel" };

export interface PrepareAssetUploadDependencies {
  findAssetByName: (folderUrl: string, fileName: string) => Promise<AssetEntry | undefined>;
  getReferences: (entry: AssetEntry) => Promise<DeleteAssetReferences>;
  requestDecision: (
    conflict: AssetUploadConflict,
    validateName: (fileName: string) => Promise<string | undefined>,
  ) => Promise<AssetUploadConflictDecision>;
  getRequiredError: () => string;
  getInvalidError: () => string;
  getCollisionError: (fileName: string) => string;
}

export async function prepareAssetUploadFiles(
  files: File[],
  folderUrl: string,
  dependencies: PrepareAssetUploadDependencies,
): Promise<File[] | undefined> {
  const preparedFiles: File[] = [];

  for (const file of files) {
    const storedEntry = await dependencies.findAssetByName(folderUrl, file.name);
    const plannedEntry = preparedFiles.some(
      (preparedFile) => normalizeAssetFileName(preparedFile.name) === normalizeAssetFileName(file.name),
    )
      ? createPlannedEntry(folderUrl, file.name)
      : undefined;
    const existingEntry = storedEntry ?? plannedEntry;

    if (!existingEntry) {
      preparedFiles.push(file);
      continue;
    }

    const references = storedEntry
      ? await dependencies.getReferences(existingEntry)
      : { referencesCount: 0, referencePages: [] };
    const decision = await dependencies.requestDecision(
      { file, existingEntry, references, source: storedEntry ? "stored" : "batch" },
      async (candidate) => validateUploadName(candidate, folderUrl, preparedFiles, dependencies),
    );

    if (decision.action === "cancel") {
      return undefined;
    }

    preparedFiles.push(decision.action === "replace" ? file : renameAssetFile(file, decision.fileName));
  }

  return preparedFiles;
}

function createPlannedEntry(folderUrl: string, fileName: string): AssetEntry {
  return {
    type: "blob",
    name: fileName,
    relativeUrl: `${folderUrl.replace(/\/$/, "")}/${fileName}`,
  };
}

export function renameAssetFile(file: File, fileName: string): File {
  return new File([file], fileName, {
    type: file.type,
    lastModified: file.lastModified,
  });
}

export function normalizeAssetFileName(value: string): string {
  return value.trim().normalize("NFC");
}

export function getAssetOverwriteMessageKey(conflict: AssetUploadConflict): AssetOverwriteMessageKey {
  if (conflict.source === "batch") {
    return "ASSET_LIBRARY.OVERWRITE.MESSAGE_BATCH_DUPLICATE";
  }

  if (conflict.references.referencesCount === 0) {
    return "ASSET_LIBRARY.OVERWRITE.MESSAGE_UNUSED";
  }

  return conflict.references.referencesCount === 1
    ? "ASSET_LIBRARY.OVERWRITE.MESSAGE_USED_ONE"
    : "ASSET_LIBRARY.OVERWRITE.MESSAGE_USED_MANY";
}

async function validateUploadName(
  fileName: string,
  folderUrl: string,
  preparedFiles: File[],
  dependencies: PrepareAssetUploadDependencies,
): Promise<string | undefined> {
  const normalized = normalizeAssetFileName(fileName);

  if (!normalized) {
    return dependencies.getRequiredError();
  }

  if (/[\\/]/.test(normalized)) {
    return dependencies.getInvalidError();
  }

  if (
    preparedFiles.some((file) => normalizeAssetFileName(file.name) === normalized) ||
    (await dependencies.findAssetByName(folderUrl, fileName))
  ) {
    return dependencies.getCollisionError(fileName);
  }

  return undefined;
}
