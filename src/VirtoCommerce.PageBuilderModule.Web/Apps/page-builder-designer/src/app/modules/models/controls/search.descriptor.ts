import { ServerRequestDescriptor } from "../http";
import { BaseControlDescriptor } from "./base-control.descriptor";

export interface SearchDescriptor extends BaseControlDescriptor {
    dataSelector?: string; // js code to select options from context
    request: ServerRequestDescriptor; // http request description to get data
    requests: { [key: string]: ServerRequestDescriptor };
    displayInfo: DisplaySearchResult[];
    nodataText: string;
    debounceTime?: number;
    button: boolean | string;
}

export interface DisplaySearchResult {
    label: string;
    path?: string;
    type?: 'text' | 'image';
}
