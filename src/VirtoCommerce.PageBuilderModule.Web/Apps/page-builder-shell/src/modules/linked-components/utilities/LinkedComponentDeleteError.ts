export class LinkedComponentDeleteConflictError extends Error {
  readonly status = 409;

  constructor(readonly component: unknown) {
    super("The linked component is currently in use and cannot be deleted.");
    this.name = "LinkedComponentDeleteConflictError";
  }
}

export function normalizeLinkedComponentDeleteError(error: unknown, componentId: string): unknown {
  if (isLinkedComponentConflictResponse(error, componentId)) {
    return new LinkedComponentDeleteConflictError(error);
  }

  return error;
}

function isLinkedComponentConflictResponse(
  value: unknown,
  componentId: string,
): value is Record<string, unknown> & { id: string; usageCount: number } {
  return (
    typeof value === "object" &&
    value !== null &&
    "id" in value &&
    value.id === componentId &&
    "usageCount" in value &&
    typeof value.usageCount === "number" &&
    Number.isFinite(value.usageCount) &&
    value.usageCount > 0
  );
}
