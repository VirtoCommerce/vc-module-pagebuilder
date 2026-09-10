export class SharedComponentDeleteConflictError extends Error {
  readonly status = 409;

  constructor(readonly component: unknown) {
    super("The shared component is currently in use and cannot be deleted.");
    this.name = "SharedComponentDeleteConflictError";
  }
}

export function normalizeSharedComponentDeleteError(error: unknown, componentId: string): unknown {
  if (isSharedComponentConflictResponse(error, componentId)) {
    return new SharedComponentDeleteConflictError(error);
  }

  return error;
}

function isSharedComponentConflictResponse(
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
