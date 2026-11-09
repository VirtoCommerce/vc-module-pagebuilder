import { vi } from "vitest";

// The platform behind `fetch`, for the designer's api module: requests are recorded, answered by the first route whose
// method and path match (a route answers JSON, text, or a Response); an unrouted request answers 404.

export interface Request {
  method: string;
  path: string;
  query: Record<string, string>;
  headers: Record<string, string>;
  /** the body: parsed when it is JSON */
  body: unknown;
}

type Answer = unknown | Response | ((request: Request) => unknown | Response | Promise<unknown | Response>);

const ORIGIN = "https://admin.test";

export function fakePlatform() {
  const requests: Request[] = [];
  const routes: { method: string; path: string | RegExp; answer: Answer; graphql?: string }[] = [];

  const respond = (value: unknown) =>
    value instanceof Response ? value : new Response(typeof value === "string" ? value : JSON.stringify(value ?? null), { status: 200 });

  const fetch = vi.fn(async (input: string, init: RequestInit = {}) => {
    const url = new URL(input, ORIGIN);
    const text = typeof init.body === "string" ? init.body : undefined;
    let body: unknown = text;
    try {
      body = text === undefined ? undefined : JSON.parse(text);
    } catch {
      // a form or text body stays text
    }
    const request: Request = {
      method: (init.method ?? "GET").toUpperCase(),
      path: url.pathname,
      query: Object.fromEntries(url.searchParams),
      headers: Object.fromEntries(Object.entries((init.headers ?? {}) as Record<string, string>).map(([k, v]) => [k.toLowerCase(), v])),
      body,
    };
    requests.push(request);
    const route = routes.find(
      (x) =>
        x.method === request.method &&
        (typeof x.path === "string" ? x.path === request.path : x.path.test(request.path)) &&
        (!x.graphql || String((request.body as { query?: string })?.query ?? "").includes(x.graphql)),
    );
    if (!route) return new Response("not found", { status: 404 });
    return respond(typeof route.answer === "function" ? await (route.answer as (r: Request) => unknown)(request) : route.answer);
  });

  const platform = {
    requests,
    fetch,
    /** answers a request; a later route for the same request goes first */
    on(method: string, path: string | RegExp, answer: Answer) {
      routes.unshift({ method, path, answer });
      return platform;
    },
    /** answers an xAPI query naming the field (`childCategories(`) */
    graphql(field: string, answer: Answer) {
      routes.unshift({ method: "POST", path: "/graphql", answer, graphql: field });
      return platform;
    },
    /** the requests to a path (and method) */
    to: (path: string, method?: string) => requests.filter((x) => x.path === path && (!method || x.method === method)),
    install() {
      vi.stubGlobal("fetch", fetch);
      return platform;
    },
  };
  return platform;
}

export const status = (code: number, body: unknown = "") => new Response(typeof body === "string" ? body : JSON.stringify(body), { status: code });

export type FakePlatform = ReturnType<typeof fakePlatform>;

/** A theme file's version as the fake platform gives it: it names the file, and a file the theme lacks has its own */
export const themeETag = (path: string, exists = true) => `"${exists ? "" : "none:"}${path}"`;

/** The designer's theme-file read over the files a test has, by their path with the theme folder */
export const themeFileAnswer = (files: Record<string, unknown>) => (request: Request) =>
  request.query.path in files
    ? { path: request.query.path, content: JSON.stringify(files[request.query.path]), eTag: themeETag(request.query.path) }
    : { path: request.query.path, eTag: themeETag(request.query.path, false) };
