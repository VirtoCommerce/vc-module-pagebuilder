import { ValueDescriptorModel } from './value-descriptor.model';

export interface ServerRequest {
    url: string;
    method: string;
    params: any;
    value: string | (string | ValueDescriptorModel)[];
    resultField: string;
    searchField: string;
}
