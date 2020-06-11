import { BaseControlDescriptor, OptionModel } from '.';
import { OptionsRequest } from './options.request';

export interface SelectControlDescriptor extends BaseControlDescriptor {
    options: OptionModel[];
    request: OptionsRequest;
}
