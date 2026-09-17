import type { AssetEntry } from "../types";
import type { DeleteAssetReferences } from "../composables/useAssetReferences";

export interface AssetUploadConflict {
  file: File;
  existingEntry: AssetEntry;
  references: DeleteAssetReferences;
  source: "stored" | "batch";
  usageKnown: boolean;
}

export type AssetOverwriteMessageKey =
  | "ASSET_LIBRARY.OVERWRITE.MESSAGE_UNUSED"
  | "ASSET_LIBRARY.OVERWRITE.MESSAGE_USED_ONE"
  | "ASSET_LIBRARY.OVERWRITE.MESSAGE_USED_MANY"
  | "ASSET_LIBRARY.OVERWRITE.MESSAGE_BATCH_DUPLICATE"
  | "ASSET_LIBRARY.OVERWRITE.MESSAGE_USAGE_UNKNOWN";

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

    const { references, usageKnown } = await getConflictReferences(storedEntry, existingEntry, dependencies);
    const decision = await dependencies.requestDecision(
      { file, existingEntry, references, source: storedEntry ? "stored" : "batch", usageKnown },
      async (candidate) => validateUploadName(candidate, folderUrl, preparedFiles, dependencies),
    );

    if (decision.action === "cancel") {
      return undefined;
    }

    preparedFiles.push(applyUploadDecision(file, decision, storedEntry));
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

function applyUploadDecision(
  file: File,
  decision: Exclude<AssetUploadConflictDecision, { action: "cancel" }>,
  storedEntry: AssetEntry | undefined,
): File {
  if (decision.action === "upload-as") {
    return renameAssetFile(file, decision.fileName);
  }

  return storedEntry ? renameAssetFile(file, storedEntry.name) : file;
}

export function normalizeAssetFileName(value: string): string {
  // Prefer a harmless extra warning on case-sensitive providers over a silent overwrite on case-insensitive ones.
  try {
    return decodeURIComponent(value).trim().normalize("NFC").toLowerCase();
  } catch {
    return value.trim().normalize("NFC").toLowerCase();
  }
}

export function getAssetOverwriteMessageKey(conflict: AssetUploadConflict): AssetOverwriteMessageKey {
  if (conflict.source === "batch") {
    return "ASSET_LIBRARY.OVERWRITE.MESSAGE_BATCH_DUPLICATE";
  }

  if (!conflict.usageKnown) {
    return "ASSET_LIBRARY.OVERWRITE.MESSAGE_USAGE_UNKNOWN";
  }

  if (conflict.references.referencesCount === 0) {
    return "ASSET_LIBRARY.OVERWRITE.MESSAGE_UNUSED";
  }

  return conflict.references.referencesCount === 1
    ? "ASSET_LIBRARY.OVERWRITE.MESSAGE_USED_ONE"
    : "ASSET_LIBRARY.OVERWRITE.MESSAGE_USED_MANY";
}

async function getConflictReferences(
  storedEntry: AssetEntry | undefined,
  existingEntry: AssetEntry,
  dependencies: PrepareAssetUploadDependencies,
): Promise<{ references: DeleteAssetReferences; usageKnown: boolean }> {
  if (!storedEntry) {
    return { references: { referencesCount: 0, referencePages: [], usageKnown: true }, usageKnown: true };
  }

  try {
    const references = await dependencies.getReferences(existingEntry);
    return { references, usageKnown: references.usageKnown };
  } catch {
    return { references: { referencesCount: 0, referencePages: [], usageKnown: false }, usageKnown: false };
  }
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
