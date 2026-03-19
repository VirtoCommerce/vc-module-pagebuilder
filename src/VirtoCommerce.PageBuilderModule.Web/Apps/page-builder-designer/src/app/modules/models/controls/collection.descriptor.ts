import { ControlDescriptor } from './index';
import { BaseControlDescriptor } from '.';

export interface CollectionDescriptor extends BaseControlDescriptor {
    addText?: string;
    displayField?: string;
    skipRemoveConfirmation?: boolean;
    removeMessage?: string;
    maxCount?: number;
    elementDescriptor?: string;
    element: ControlDescriptor[];
}
