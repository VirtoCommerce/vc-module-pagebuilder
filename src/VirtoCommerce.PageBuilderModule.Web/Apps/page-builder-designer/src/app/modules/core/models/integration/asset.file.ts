export interface AssetFile extends File {
    data?: any;
    url?: string;
    previewUrl: string | null;
    uploaded?: boolean;
    uploading?: boolean;

    assetName: string;
    progress?: number;
    error: string | null;
}
