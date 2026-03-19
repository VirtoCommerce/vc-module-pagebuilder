import { BaseControlDescriptor } from './base-control.descriptor';

export interface StringDescriptor extends BaseControlDescriptor {
    multiline?: boolean;
    minRowsCount?: number;
    maxRowsCount?: number;
    // mask?: string; // todo:
}
