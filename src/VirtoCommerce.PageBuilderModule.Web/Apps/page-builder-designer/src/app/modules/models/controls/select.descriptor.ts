import { ServerRequestDescriptor } from '@models/index';
import { BaseControlDescriptor } from "./base-control.descriptor";
import { SelectOptionModel } from './select-option.model';

export interface OptionsRequest extends ServerRequestDescriptor {
    group: string;
    label: string;
    value?: string; // property name to use as value or empty if value is the object itself
}

export interface SelectDescriptor extends BaseControlDescriptor {
    multiple?: boolean;
    options?: SelectOptionModel[];
    optionsSelector?: string; // js code to select options from context
    request: OptionsRequest; // http request description to get options
    equalKey?: string; // property name to compare options
    displayField?: string; // property name to display
    searchable?: boolean;
    required?: boolean;
}
