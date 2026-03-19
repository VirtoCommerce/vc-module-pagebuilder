import { BaseControlDescriptor } from './base-control.descriptor';

export interface StringDescriptor extends BaseControlDescriptor {
    multiline?: boolean;
    // mask?: string; // todo:
}
