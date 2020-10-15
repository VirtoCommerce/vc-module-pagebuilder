import { BaseControlDescriptor, OptionModel } from '.';
import { OptionsRequest } from './options.request';

export interface SelectControlDescriptor extends BaseControlDescriptor {
    options: OptionModel[];
    request: OptionsRequest;
    equalKey?: string;
    filterList: boolean; // // todo: for local options
    cacheRequest?: boolean;
}
