import { vi } from "vitest";

export interface Request {
  method: string;
  path: string;
  query: Record<string, string>;
  headers: Record<string, string>;
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
    on(method: string, path: string | RegExp, answer: Answer) {
      routes.unshift({ method, path, answer });
      return platform;
    },
    graphql(field: string, answer: Answer) {
      routes.unshift({ method: "POST", path: "/graphql", answer, graphql: field });
      return platform;
    },
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

export const themeETag = (path: string, exists = true) => `"${exists ? "" : "none:"}${path}"`;

export const themeFileAnswer = (files: Record<string, unknown>) => (request: Request) =>
  request.query.path in files
    ? { path: request.query.path, content: JSON.stringify(files[request.query.path]), eTag: themeETag(request.query.path) }
    : { path: request.query.path, eTag: themeETag(request.query.path, false) };
