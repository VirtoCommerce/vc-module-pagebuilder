import { ValueDescriptorModel } from './value-descriptor.model';

export interface OptionsRequest {
    url: string;
    method: string;
    params: any;
    group: string;
    label: string;
    value: string | (string | ValueDescriptorModel)[];
}