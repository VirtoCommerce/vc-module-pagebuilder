import { BaseControlDescriptor } from "./base-control.descriptor";
import { UploadAssetDescriptor } from './upload-asset.descriptor';
import { ControlDescriptor } from './index';
import { AssetsRequest } from "../http";

export interface FilesDescriptor extends BaseControlDescriptor, UploadAssetDescriptor {
    multiple?: boolean;
    sortable?: boolean;
    accept?: string;
    maxFileSize?: number;
    collapseThreshold?: number;
    collapseCount?: number;
    skipRemoveConfirmation?: boolean;
    removeMessage?: string;

    element: ControlDescriptor[];
    elementDescriptor?: string;
}
