import { ValueDescriptorModel } from './value-descriptor.model';

export interface ServerRequestDescriptor {
    url: string;
    method: string;
    body: any;
    cacheContextPath: string;
    response: ServerResponseDescriptor;
    // value: string | (string | ValueDescriptorModel)[];
    // resultField: string;
    // searchField: string;
}

export interface ServerResponseDescriptor {
    result: string; // path to result field
    isArray: boolean; // result should be array
    value: string | (string | ValueDescriptorModel)[];
}