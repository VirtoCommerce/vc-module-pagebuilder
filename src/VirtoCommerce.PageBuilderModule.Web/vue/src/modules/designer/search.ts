import type { Field, RemoteRequest } from "./types";
import { pick } from "./schema";

// The `search` field of the Angular designer (core/controls/search): the editor types a query (or presses a button),
// the field's request runs with it as `__searchQuery`, and what it answers is kept in the setting, for the storefront
// to show without asking again.

/** What a `search` field shows of the result: a value at a dotted path, as text or as an image */
export interface SearchDisplay {
  label: string;
  path?: string;
  type?: "text" | "image";
}

export interface SearchField extends Field {
  /** one request; its answer is kept as `value` */
  request?: RemoteRequest;
  /** several, run in turn, each answer kept under its key; a request sees the answers so far as `item` */
  requests?: Record<string, RemoteRequest>;
  displayInfo?: SearchDisplay[];
  nodataText?: string;
  /** how long typing pauses before the search runs, in ms */
  debounceTime?: number;
  /** a button runs the search instead of typing; a string is its text */
  button?: boolean | string;
}

/** The setting's value: the query, whether nothing came back, and the answers */
export interface SearchValue {
  __nodata: boolean;
  __searchQuery: string | null;
  [key: string]: unknown;
}

/** The Angular designer's pause before a typed query runs */
export const SEARCH_DEBOUNCE_MS = 1000;

/** A request of the field, run with the context; resolves to its answer at `response.result` */
export type RunRequest = (request: RemoteRequest, context: Record<string, unknown>) => Promise<unknown>;

/** Runs the field's request (or requests, in turn) for the query. A failed request fails the search. */
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

/** The answer at a display row's path, as the Angular designer read it from the single request's answer */
export const searchDisplayValue = (value: unknown, display: SearchDisplay): unknown =>
  display.path ? pick((value as SearchValue | undefined)?.value, display.path) : undefined;

/** The answer a request gives: the response at its `response.result` dotted path, else the response */
export const requestResult = (request: RemoteRequest, response: unknown): unknown => (request.response?.result ? pick(response, request.response.result) : response);
