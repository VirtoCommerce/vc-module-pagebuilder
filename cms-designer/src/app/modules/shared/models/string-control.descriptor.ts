import { BaseControlDescriptor } from '.';

export interface StringControlDescriptor extends BaseControlDescriptor {
    placeholder?: string;
    multiline?: boolean;
}
