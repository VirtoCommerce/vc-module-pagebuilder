// Plain JSON data as the designer reads it from theme files, the server and the preview: the shape checks and a deep
// copy every part uses.

/** An object, not an array or null */
export const isRecord = (value: unknown): value is Record<string, unknown> => !!value && typeof value === "object" && !Array.isArray(value);

/** A string with more than white space */
export const isText = (value: unknown): value is string => typeof value === "string" && !!value.trim();

/** A list of strings, empty or not, the strings empty or not: a list's shape, not its content */
export const isTextList = (value: unknown): value is string[] => Array.isArray(value) && value.every((x) => typeof x === "string");

/** A deep copy of JSON data (also of reactive proxies, which structuredClone refuses) */
export const clone = <T>(value: T): T => (value === undefined ? value : (JSON.parse(JSON.stringify(value)) as T));
