import { ServerRequestDescriptor, BaseControlDescriptor } from '.';

export interface SearchControlDescriptor extends BaseControlDescriptor {
    request: ServerRequestDescriptor;
    displayInfo: { label: string; key: string; type?: 'text'|'image' }[];
    nodataText: string;
}
