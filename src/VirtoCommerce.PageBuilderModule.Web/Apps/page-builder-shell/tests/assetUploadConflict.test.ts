import assert from "node:assert/strict";
import test from "node:test";
import type { AssetEntry } from "../src/modules/asset-library/types";
import {
  getAssetOverwriteMessageKey,
  normalizeAssetFileName,
  prepareAssetUploadFiles,
  type AssetUploadConflict,
  type AssetUploadConflictDecision,
  type PrepareAssetUploadDependencies,
} from "../src/modules/asset-library/utilities/assetUpload";

const folderUrl = "/stores/store/Page Builder";
const existingEntry: AssetEntry = {
  type: "blob",
  name: "hero.jpg",
  relativeUrl: `${folderUrl}/hero.jpg`,
};

test("prepareAssetUploadFiles keeps a different extension without confirmation", async () => {
  let confirmations = 0;
  const result = await prepareAssetUploadFiles(
    [createFile("hero.png")],
    folderUrl,
    createDependencies({
      findAssetByName: async (_folder, fileName) => (fileName === existingEntry.name ? existingEntry : undefined),
      requestDecision: async () => {
        confirmations += 1;
        return { action: "replace" };
      },
    }),
  );

  assert.deepEqual(
    result?.map((file) => file.name),
    ["hero.png"],
  );
  assert.equal(confirmations, 0);
});

test("prepareAssetUploadFiles keeps the original name after Replace", async () => {
  const result = await prepareAssetUploadFiles(
    [createFile("hero.jpg")],
    folderUrl,
    createDependencies({
      findAssetByName: async () => existingEntry,
      requestDecision: async (conflict) => {
        assert.equal(conflict.references.referencesCount, 2);
        assert.equal(conflict.source, "stored");
        assert.equal(conflict.usageKnown, true);
        return { action: "replace" };
      },
    }),
  );

  assert.deepEqual(
    result?.map((file) => file.name),
    ["hero.jpg"],
  );
});

test("prepareAssetUploadFiles uses the stored name when Replace matches case-insensitively", async () => {
  const result = await prepareAssetUploadFiles(
    [createFile("Hero.jpg")],
    folderUrl,
    createDependencies({
      findAssetByName: async () => existingEntry,
      requestDecision: async () => ({ action: "replace" }),
    }),
  );

  assert.deepEqual(
    result?.map((file) => file.name),
    ["hero.jpg"],
  );
});

test("prepareAssetUploadFiles renames a file only after the candidate is available", async () => {
  const checkedNames: string[] = [];
  const result = await prepareAssetUploadFiles(
    [createFile("hero.jpg")],
    folderUrl,
    createDependencies({
      findAssetByName: async (_folder, fileName) => {
        checkedNames.push(fileName);
        return fileName === "hero.jpg" ? existingEntry : undefined;
      },
      requestDecision: async (_conflict, validateName) => {
        assert.equal(await validateName("hero.jpg"), "hero.jpg collides");
        assert.equal(await validateName("hero-new.jpg"), undefined);
        return { action: "upload-as", fileName: "hero-new.jpg" };
      },
    }),
  );

  assert.deepEqual(
    result?.map((file) => file.name),
    ["hero-new.jpg"],
  );
  assert.deepEqual(checkedNames, ["hero.jpg", "hero.jpg", "hero-new.jpg"]);
});

test("prepareAssetUploadFiles cancels the complete batch before returning files", async () => {
  const result = await prepareAssetUploadFiles(
    [createFile("new.jpg"), createFile("hero.jpg")],
    folderUrl,
    createDependencies({
      findAssetByName: async (_folder, fileName) => (fileName === "hero.jpg" ? existingEntry : undefined),
      requestDecision: async () => ({ action: "cancel" }),
    }),
  );

  assert.equal(result, undefined);
});

test("prepareAssetUploadFiles confirms duplicate names inside one batch before uploading", async () => {
  const decisions: AssetUploadConflict[] = [];
  const dependencies = createDependencies({
    requestDecision: async (conflict) => {
      decisions.push(conflict);
      return { action: "upload-as", fileName: "second-copy.png" };
    },
  });

  const prepared = await prepareAssetUploadFiles(
    [new File(["first"], "same.png"), new File(["second"], "same.png")],
    "/assets",
    dependencies,
  );

  assert.equal(decisions.length, 1);
  assert.equal(decisions[0]?.source, "batch");
  assert.equal(decisions[0]?.usageKnown, true);
  assert.equal(decisions[0]?.references.referencesCount, 0);
  assert.equal(getAssetOverwriteMessageKey(decisions[0]!), "ASSET_LIBRARY.OVERWRITE.MESSAGE_BATCH_DUPLICATE");
  assert.deepEqual(
    prepared?.map((file) => file.name),
    ["same.png", "second-copy.png"],
  );
});

test("prepareAssetUploadFiles degrades to usage unknown when reference lookup fails", async () => {
  let conflict: AssetUploadConflict | undefined;
  const result = await prepareAssetUploadFiles(
    [createFile("hero.jpg")],
    folderUrl,
    createDependencies({
      findAssetByName: async () => existingEntry,
      getReferences: async () => {
        throw new Error("reference service unavailable");
      },
      requestDecision: async (value) => {
        conflict = value;
        return { action: "replace" };
      },
    }),
  );

  assert.equal(conflict?.usageKnown, false);
  assert.equal(conflict && getAssetOverwriteMessageKey(conflict), "ASSET_LIBRARY.OVERWRITE.MESSAGE_USAGE_UNKNOWN");
  assert.deepEqual(
    result?.map((file) => file.name),
    ["hero.jpg"],
  );
});

test("prepareAssetUploadFiles asks again when a later file reuses a replaced batch name", async () => {
  const conflicts: AssetUploadConflict[] = [];
  const result = await prepareAssetUploadFiles(
    [createFile("same.jpg"), createFile("same.jpg"), createFile("SAME.jpg")],
    folderUrl,
    createDependencies({
      requestDecision: async (conflict) => {
        conflicts.push(conflict);
        return { action: "replace" };
      },
    }),
  );

  assert.equal(conflicts.length, 2);
  assert.deepEqual(
    result?.map((file) => file.name),
    ["same.jpg", "same.jpg", "SAME.jpg"],
  );
});

test("normalizeAssetFileName catches encoded, whitespace, Unicode and case-only collisions", () => {
  assert.equal(normalizeAssetFileName("  HERO%20Banner.PNG  "), "hero banner.png");
  assert.equal(normalizeAssetFileName("Cafe\u0301.png"), "café.png");
});

function createFile(name: string): File {
  return new File([name], name, { type: "image/jpeg", lastModified: 123 });
}

function createDependencies(overrides: Partial<PrepareAssetUploadDependencies>): PrepareAssetUploadDependencies {
  return {
    findAssetByName: async () => undefined,
    getReferences: async () => ({
      referencesCount: 2,
      referencePages: [{ id: "page-1" }, { id: "page-2" }],
      usageKnown: true,
    }),
    requestDecision: async () => ({ action: "cancel" }) as AssetUploadConflictDecision,
    getRequiredError: () => "required",
    getInvalidError: () => "invalid",
    getCollisionError: (fileName) => `${fileName} collides`,
    ...overrides,
  };
}
