import type { Field, RemoteRequest } from "./types";
import { pick } from "./schema";

// The answer is kept in the setting so that the storefront can show it without asking again.

export interface SearchDisplay {
  label: string;
  path?: string;
  type?: "text" | "image";
}

export interface SearchField extends Field {
  request?: RemoteRequest;
  requests?: Record<string, RemoteRequest>;
  displayInfo?: SearchDisplay[];
  nodataText?: string;
  debounceTime?: number;
  button?: boolean | string;
}

export interface SearchValue {
  __nodata: boolean;
  __searchQuery: string | null;
  [key: string]: unknown;
}

export const SEARCH_DEBOUNCE_MS = 1000;

export type RunRequest = (request: RemoteRequest, context: Record<string, unknown>) => Promise<unknown>;

export async function runSearch(field: SearchField, context: Record<string, unknown>, query: string | null, run: RunRequest): Promise<SearchValue> {
  let value: SearchValue = { __nodata: true, __searchQuery: query };
  const searchContext: Record<string, unknown> = { ...context, __searchQuery: query };
  const requests: [string, RemoteRequest][] = field.request ? [["value", field.request]] : Object.entries(field.requests ?? {});
  for (const [key, request] of requests) {
    const result = await run(request, { ...searchContext, item: value });
    value = { ...value, __nodata: value.__nodata && !result, [key]: result };
  }
  return value;
}

export const searchDisplayValue = (value: unknown, display: SearchDisplay): unknown =>
  display.path ? pick((value as SearchValue | undefined)?.value, display.path) : undefined;

export const requestResult = (request: RemoteRequest, response: unknown): unknown => (request.response?.result ? pick(response, request.response.result) : response);
