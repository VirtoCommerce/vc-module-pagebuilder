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
}
