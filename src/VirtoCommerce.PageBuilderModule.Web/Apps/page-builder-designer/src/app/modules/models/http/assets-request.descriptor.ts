import { ServerRequestDescriptor } from "./server-request.descriptor";

export interface AssetsRequest extends ServerRequestDescriptor {
    randomizeAssetName?: boolean;
    resultTemplate?: string;
    previewTemplate?: string;
}
