import { ref } from "vue";
import type { AssetEntry } from "../types";

interface ReadonlyRef<T> {
  readonly value: T;
}

export function useAssetLibraryDragDrop(
  canCreate: ReadonlyRef<boolean>,
  uploadFiles: (files: FileList | File[] | undefined, folderUrl?: string) => Promise<boolean>,
) {
  const isDraggingOverSurface = ref(false);
  const draggedFolderUrl = ref<string>();

  function getEntryDropFolderUrl(entry: AssetEntry): string | undefined {
    if (entry.type !== "folder") {
      return undefined;
    }

    return entry.relativeUrl || entry.url;
  }

  function handleSurfaceDrag(event: DragEvent) {
    if (!canCreate.value || !hasDraggedFiles(event)) {
      return;
    }

    markDropEffect(event);
    isDraggingOverSurface.value = true;
  }

  function handleSurfaceDragLeave(event: DragEvent) {
    if (isLeavingCurrentTarget(event)) {
      return;
    }

    resetDragState();
  }

  async function handleSurfaceDrop(event: DragEvent) {
    const files = event.dataTransfer?.files;
    resetDragState();

    await uploadFiles(files);
  }

  function handleFolderDrag(entry: AssetEntry, event: DragEvent) {
    const folderUrl = getEntryDropFolderUrl(entry);

    if (!canCreate.value || !hasDraggedFiles(event)) {
      return;
    }

    markDropEffect(event);
    isDraggingOverSurface.value = true;
    draggedFolderUrl.value = folderUrl;
  }

  function handleFolderDragLeave(entry: AssetEntry, event: DragEvent) {
    if (isLeavingCurrentTarget(event)) {
      return;
    }

    if (draggedFolderUrl.value === getEntryDropFolderUrl(entry)) {
      draggedFolderUrl.value = undefined;
    }
  }

  async function handleFolderDrop(entry: AssetEntry, event: DragEvent) {
    const files = event.dataTransfer?.files;
    const folderUrl = getEntryDropFolderUrl(entry);
    resetDragState();

    await uploadFiles(files, folderUrl);
  }

  function markDropEffect(event: DragEvent) {
    if (event.dataTransfer) {
      event.dataTransfer.dropEffect = canCreate.value && hasDraggedFiles(event) ? "copy" : "none";
    }
  }

  function hasDraggedFiles(event: DragEvent): boolean {
    return Array.from(event.dataTransfer?.types ?? []).includes("Files");
  }

  function isLeavingCurrentTarget(event: DragEvent): boolean {
    const currentTarget = event.currentTarget as HTMLElement | null;
    const nextTarget = event.relatedTarget;

    return !!currentTarget && nextTarget instanceof Node && currentTarget.contains(nextTarget);
  }

  function resetDragState() {
    isDraggingOverSurface.value = false;
    draggedFolderUrl.value = undefined;
  }

  return {
    isDraggingOverSurface,
    draggedFolderUrl,
    getEntryDropFolderUrl,
    handleSurfaceDrag,
    handleSurfaceDragLeave,
    handleSurfaceDrop,
    handleFolderDrag,
    handleFolderDragLeave,
    handleFolderDrop,
  };
}
