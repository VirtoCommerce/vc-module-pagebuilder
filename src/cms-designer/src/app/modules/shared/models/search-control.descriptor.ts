import { ServerRequest, BaseControlDescriptor } from '.';

export interface SearchControlDescriptor extends BaseControlDescriptor {
    request: ServerRequest;
    displayInfo: { label: string; key: string; type?: 'text'|'image' }[];
    nodataText: string;
}
