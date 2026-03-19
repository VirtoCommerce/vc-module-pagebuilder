import { AssetsRequest } from "../http";
import { UploadAssetDescriptor } from './upload-asset.descriptor';
import { BaseControlDescriptor } from "./base-control.descriptor";

export interface MarkdownDescriptor extends BaseControlDescriptor, UploadAssetDescriptor {
    resultType: 'markdown' | 'html' | 'mixed';
    styles: string[] | string | null;
    editorOptions?: any;
}
