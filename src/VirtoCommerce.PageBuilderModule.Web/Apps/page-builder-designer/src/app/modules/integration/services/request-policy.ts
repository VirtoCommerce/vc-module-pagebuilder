/**
 * What a request taken from a settings or schema descriptor is allowed to reach.
 *
 * Those descriptors (`url`, `method`, `body`, `init`, response `selector`) come from the theme's
 * settings and schemas, which anyone who can save a theme can write, and they run in the admin's
 * session. So a descriptor may only talk to the platform that serves the designer:
 *
 * - every request stays on the designer's own origin, so nothing a descriptor returns can be sent
 *   to another host;
 * - a write (anything other than GET) may only reach the endpoints the bundled settings and the
 *   shipped schemas actually post to, so a descriptor cannot drive an arbitrary platform mutation
 *   (creating a user, assigning a role) from the admin's session.
 *
 * A read on the platform origin is left alone: it cannot leave the platform, and which pages or
 * settings it reads is already the admin's own access.
 */

// Writes the bundled settings and the vc-frontend schemas make. A path matches when it equals an
// entry or continues it after a "/", so "/api/assets" also covers "/api/assets/folder" but not a
// sibling like "/api/assets-export".
const WRITABLE_PATHS = [
    '/graphql',
    '/api/pagebuilder/save',
    '/api/page-builder-assets',
    '/api/page-builder-pages',
    '/api/assets',
    '/api/content',
    '/api/platform/security/users/search',
];

function samePlatformOrigin(url: string, documentUrl: string): boolean {
    try {
        return new URL(url, documentUrl).origin === new URL(documentUrl).origin;
    } catch {
        return false;
    }
}

function isWritablePath(url: string, documentUrl: string): boolean {
    let path: string;
    try {
        path = new URL(url, documentUrl).pathname.replace(/\/+$/, '');
    } catch {
        return false;
    }

    return WRITABLE_PATHS.some(allowed => path === allowed || path.startsWith(allowed + '/'));
}

/**
 * Whether a descriptor request may be sent. `method` defaults to GET, as it does when a descriptor
 * leaves it out.
 */
export function isAllowedRequest(url: string, method: string | null | undefined, documentUrl: string): boolean {
    if (!url || !samePlatformOrigin(url, documentUrl)) {
        return false;
    }

    return (method ?? 'GET').toUpperCase() === 'GET' || isWritablePath(url, documentUrl);
}
