import { SelectValueDescriptor } from "./select-value.descriptor";

export interface ServerRequestDescriptor {
    url: string;
    method: string;
    body: any;
    form?: any;
    options?: any;
    response?: ServerResponseDescriptor;
    cacheable?: boolean;

    init?: boolean | string;
    fallbackValue?: any; // value to use when request failed
    // cacheContextPath?: string;
    // value: string | (string | ValueDescriptorModel)[];
    // resultField: string;
    // searchField: string;
}

export interface ServerResponseDescriptor {
    selector?: string; // script to eval over response
    result: string; // path to result field (jsonpath)
    isArray: boolean; // result should be array
    value: string | (string | SelectValueDescriptor)[];
}

