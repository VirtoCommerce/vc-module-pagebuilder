export interface AssetLibraryContext {
    storeId?: string | string[];
    location?: {
        params?: {
            storeId?: string | string[];
        };
    };
    template?: any;
    model?: any;
}

export interface AssetLibraryEntry {
    type: 'folder' | 'blob';
    name: string;
    url?: string;
    relativeUrl?: string;
    contentType?: string;
    size?: number;
    modifiedDate?: string;
    createdDate?: string;
}

export interface AssetLibrarySearchResult {
    totalCount: number;
    results: AssetLibraryEntry[];
}

export interface AssetLibraryReferencePage {
    id?: string;
    name?: string;
    permalink?: string;
    cultureName?: string;
}

export interface AssetLibraryReference {
    assetUrl?: string;
    referencesCount?: number;
    pages?: AssetLibraryReferencePage[];
}

export interface AssetLibraryReferencesSearchResult {
    totalCount: number;
    results: AssetLibraryReference[];
}

export interface AssetLibraryLabels {
    title: string;
    rootBreadcrumb: string;
    choose: string;
    chooseOrDrop: string;
    fileLabel: string;
    imageLabel: string;
    upload: string;
    uploading: string;
    searchPlaceholder: string;
    assetsCounter: string;
    loading: string;
    empty: string;
    cancel: string;
    select: string;
    storeRequired: string;
    uploadError: string;
    loadError: string;
    fileTooLarge: string;
    overwriteTitle: string;
    overwriteUnused: string;
    overwriteUsedOne: string;
    overwriteUsedMany: string;
    overwriteBatchDuplicate: string;
    overwriteUsageUnknown: string;
    affectedPages: string;
    uploadAs: string;
    replace: string;
    fileNameRequired: string;
    fileNameInvalid: string;
    fileNameCollision: string;
    uploadCanceled: string;
}
