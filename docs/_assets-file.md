# Asset File

описывает загруженный или прикрепленный в редакторе файл

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

todo: посмотреть как используется

примеры
