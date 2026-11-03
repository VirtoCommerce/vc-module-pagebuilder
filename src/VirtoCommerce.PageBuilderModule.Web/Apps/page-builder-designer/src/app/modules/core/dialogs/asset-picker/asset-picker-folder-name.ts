import type { AssetLibraryLabels } from '@core/services';

// Keep the rule order and accepted names aligned with vc-module-assets' BlobFolderValidator.
export function getFolderNameError(name: string): keyof AssetLibraryLabels['folderNameErrors'] | null {
    const value = name.trim();
    if (!value) return null;
    if (value.length < 3) return 'minLength';
    if (value.length > 63) return 'maxLength';
    if (value.startsWith('-')) return 'dashStart';
    if (value.endsWith('-')) return 'dashEnd';
    if (value.includes('--')) return 'dashConsecutive';
    if (/[^0-9a-z -]/.test(value)) return 'invalidCharacters';
    return null;
}
