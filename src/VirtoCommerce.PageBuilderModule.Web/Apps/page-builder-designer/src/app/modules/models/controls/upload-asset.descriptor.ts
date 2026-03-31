import { AssetsRequest } from "../http";

export interface UploadAssetDescriptor {
    urlField?: string;
    filenameField?: string;
    uploadAssetsRequest?: AssetsRequest | string;
}
