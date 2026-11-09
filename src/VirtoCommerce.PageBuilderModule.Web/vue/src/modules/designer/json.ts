export const isRecord = (value: unknown): value is Record<string, unknown> => !!value && typeof value === "object" && !Array.isArray(value);

export const isText = (value: unknown): value is string => typeof value === "string" && !!value.trim();

export const isTextList = (value: unknown): value is string[] => Array.isArray(value) && value.every((x) => typeof x === "string");

/** structuredClone refuses reactive proxies. */
export const clone = <T>(value: T): T => (value === undefined ? value : (JSON.parse(JSON.stringify(value)) as T));
