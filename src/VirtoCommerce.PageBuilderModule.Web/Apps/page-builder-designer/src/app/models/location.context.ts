export interface LocationContext {
    url: string;
    params: Record<string, string | string[]>;
    path: string;
    host: string;
    protocol: string;
    hash: string;
    hashPath: string | null;
    origin: string;
}
