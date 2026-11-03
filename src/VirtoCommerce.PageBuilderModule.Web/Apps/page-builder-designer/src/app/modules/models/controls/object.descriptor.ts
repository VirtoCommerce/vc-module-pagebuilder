import { ControlDescriptor } from './index';
import { BaseControlDescriptor } from '.';

export interface ObjectDescriptor extends BaseControlDescriptor {
    title?: string;
    displayField: string;
    element: ControlDescriptor[];
    elementDescriptor?: string;
}
