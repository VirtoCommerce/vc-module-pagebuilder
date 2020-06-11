import { ValueDescriptorModel } from './value-descriptor.model';

export interface OptionsRequest {
    url: string;
    type: string;
    params: any;
    labelDescriptor: string;
    valueDescriptor: string | (string | ValueDescriptorModel)[];
}