const imageFileNamePattern = /\.(apng|avif|bmp|gif|ico|jpg|jpeg|png|svg|webp)$/i;

export function formatAssetSize(size: number): string {
    if (size < 1024) {
        return `${size} B`;
    }

    if (size < 1024 * 1024) {
        return `${Math.round(size / 1024)} KB`;
    }

    return `${(size / 1024 / 1024).toFixed(1)} MB`;
}

export function isImageFileName(fileName: string): boolean {
    return imageFileNamePattern.test(fileName);
}

export function matchesAcceptFile(file: Pick<File, 'name' | 'type'>, acceptedTypes: string[]): boolean {
    if (!acceptedTypes.length) {
        return true;
    }

    const contentType = file.type.toLowerCase();
    const fileName = file.name.toLowerCase();

    return acceptedTypes.some(type => {
        const accept = type.toLowerCase();

        if (accept.endsWith('/*')) {
            return contentType.startsWith(accept.slice(0, -1))
                || (accept === 'image/*' && isImageFileName(fileName));
        }

        if (accept.startsWith('.')) {
            return fileName.endsWith(accept);
        }

        return contentType === accept;
    });
}

export function safeDecodeURIComponent(value: string): string {
    try {
        return decodeURIComponent(value);
    } catch {
        return value;
    }
}

export function normalizeAssetFileName(value: string): string {
    // Prefer a harmless extra warning on case-sensitive providers over a silent overwrite on case-insensitive ones.
    return safeDecodeURIComponent(value).trim().normalize('NFC').toLowerCase();
}

export function formatAssetLabel(template: string, values: Record<string, string>): string {
    return Object.entries(values).reduce((result, [key, value]) => result.replaceAll(`{${key}}`, value), template);
}

export function getAssetOverwriteConsequenceMessage(data: {
    source: 'stored' | 'batch';
    usageKnown: boolean;
    assetName: string;
    referencesCount?: number;
    labels: {
        overwriteUnused: string;
        overwriteUsedOne: string;
        overwriteUsedMany: string;
        overwriteBatchDuplicate: string;
        overwriteUsageUnknown: string;
    };
}): string {
    const count = data.referencesCount ?? 0;
    let template = data.labels.overwriteUsedMany;

    if (data.source === 'batch') {
        template = data.labels.overwriteBatchDuplicate;
    } else if (!data.usageKnown) {
        template = data.labels.overwriteUsageUnknown;
    } else if (count === 0) {
        template = data.labels.overwriteUnused;
    } else if (count === 1) {
        template = data.labels.overwriteUsedOne;
    }

    return formatAssetLabel(template, {
        name: data.assetName,
        count: count.toString(),
    });
}

export function toPublicAssetUrl(value: string): string {
    if (/^(?:[a-z][a-z\d+\-.]*:)?\/\//i.test(value) || value.startsWith('data:')) {
        return value;
    }

    const normalized = ensureLeadingSlash(value.trim());
    return normalized.toLowerCase().startsWith('/assets/')
        ? normalized
        : `/assets${normalized}`;
}

export function toAssetPreviewUrl(value: string, modifiedDate?: string): string {
    const publicUrl = toPublicAssetUrl(value);
    if (!modifiedDate) {
        return publicUrl;
    }

    const separator = publicUrl.includes('?') ? '&' : '?';
    return `${publicUrl}${separator}t=${encodeURIComponent(modifiedDate)}`;
}

function ensureLeadingSlash(value: string): string {
    return value.startsWith('/') ? value : `/${value}`;
}
