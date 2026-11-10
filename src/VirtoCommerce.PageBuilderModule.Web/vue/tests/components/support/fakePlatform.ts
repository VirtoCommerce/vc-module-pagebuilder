import { vi } from "vitest";

export interface Request {
  method: string;
  origin: string;
  path: string;
  query: Record<string, string>;
  searchParams: URLSearchParams;
  headers: Record<string, string>;
  credentials?: RequestCredentials;
  body: unknown;
}

type Answer = unknown | Response | ((request: Request) => unknown | Response | Promise<unknown | Response>);

// The admin app is served by the platform, so a relative URL goes to the page's own origin.
const platformOrigin = () => window.location.origin;

// A bare path is the platform's; another origin is routed and looked up by its full URL.
const matches = (path: string | RegExp, request: Request) => {
  if (typeof path !== "string") return request.origin === platformOrigin() && path.test(request.path);
  const url = new URL(path, platformOrigin());
  return url.origin === request.origin && url.pathname === request.path;
};

const escapeRegExp = (text: string) => text.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");

export function fakePlatform() {
  const requests: Request[] = [];
  const routes: { method: string; path: string | RegExp; answer: Answer; graphql?: RegExp }[] = [];

  const respond = (value: unknown) =>
    value instanceof Response ? value : new Response(typeof value === "string" ? value : JSON.stringify(value ?? null), { status: 200 });

  const fetch = vi.fn(async (input: string, init: RequestInit = {}) => {
    const url = new URL(input, platformOrigin());
    const text = typeof init.body === "string" ? init.body : undefined;
    let body: unknown = text;
    try {
      body = text === undefined ? undefined : JSON.parse(text);
    } catch {
    }
    const request: Request = {
      method: (init.method ?? "GET").toUpperCase(),
      origin: url.origin,
      path: url.pathname,
      query: Object.fromEntries(url.searchParams),
      searchParams: url.searchParams,
      headers: Object.fromEntries(new Headers(init.headers)),
      credentials: init.credentials,
      body,
    };
    requests.push(request);
    const route = routes.find(
      (x) =>
        x.method === request.method &&
        matches(x.path, request) &&
        (!x.graphql || x.graphql.test(String((request.body as { query?: string })?.query ?? ""))),
    );
    // Not a 404: production reads a 404 as "nothing there", which would hide a mistyped URL.
    if (!route) return new Response(`unrouted ${request.method} ${url.origin}${url.pathname}`, { status: 501 });
    return respond(typeof route.answer === "function" ? await (route.answer as (r: Request) => unknown)(request) : route.answer);
  });

  const platform = {
    requests,
    fetch,
    on(method: string, path: string | RegExp, answer: Answer) {
      routes.unshift({ method, path, answer });
      return platform;
    },
    graphql(field: string, answer: Answer) {
      routes.unshift({ method: "POST", path: "/graphql", answer, graphql: new RegExp(`(?<![\\w$])${escapeRegExp(field)}`) });
      return platform;
    },
    to: (path: string, method?: string) => requests.filter((x) => matches(path, x) && (!method || x.method === method)),
    install() {
      vi.stubGlobal("fetch", fetch);
      return platform;
    },
  };
  return platform;
}

export const status = (code: number, body: unknown = "", headers?: HeadersInit) => {
  if (code === 401) throw new Error("The fake platform does not model the shell's session check that a 401 sets off.");
  const empty = code === 204 || code === 205 || code === 304;
  return new Response(empty ? null : typeof body === "string" ? body : JSON.stringify(body), { status: code, headers });
};

export type FakePlatform = ReturnType<typeof fakePlatform>;

export const themeETag = (path: string, exists = true) => `"${exists ? "" : "none:"}${path}"`;

export const themeFileAnswer = (files: Record<string, unknown>) => (request: Request) =>
  request.query.path in files
    ? { path: request.query.path, content: JSON.stringify(files[request.query.path]), eTag: themeETag(request.query.path) }
    : { path: request.query.path, eTag: themeETag(request.query.path, false) };
