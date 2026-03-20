import { BaseControlDescriptor } from './base-control.descriptor';

export interface DisplayTextDescriptor extends BaseControlDescriptor {
    type: 'header' | 'paragraph';
    content: string;
}
