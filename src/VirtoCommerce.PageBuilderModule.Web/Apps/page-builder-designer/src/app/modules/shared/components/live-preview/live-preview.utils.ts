/**
 * Tells whether a resolved `fullPreviewUrl` may be used as the source of the preview iframe.
 *
 * `fullPreviewUrl` is a template built from the store settings, by default
 * `{{=combine(settings.storefrontUrl, settings.previewPath)}}?ep={{location.origin}}`. When those
 * settings cannot be resolved - the session expired, the store has no URL configured - the template
 * collapses into a relative string such as `?ep=https://admin`. A relative source makes the iframe
 * load the designer document itself, so the designer renders recursively inside its own preview
 * (VCST-5847). Only an absolute storefront address is accepted; it is also what `postMessage` needs
 * as a target origin.
 */
export function isUsablePreviewUrl(url: unknown, documentUrl: string): url is string {
    if (typeof url !== 'string') {
        return false;
    }

    const value = url.trim();
    if (!value || value.includes('{{')) {
        return false;
    }

    let previewUrl: URL;
    try {
        previewUrl = new URL(value);
    } catch {
        return false; // relative, so it would point back at the designer
    }

    if (previewUrl.protocol !== 'http:' && previewUrl.protocol !== 'https:') {
        return false;
    }

    // a storefront URL that resolves to the designer document itself is the recursion as well
    const documentLocation = new URL(documentUrl);
    return previewUrl.origin !== documentLocation.origin || previewUrl.pathname !== documentLocation.pathname;
}
