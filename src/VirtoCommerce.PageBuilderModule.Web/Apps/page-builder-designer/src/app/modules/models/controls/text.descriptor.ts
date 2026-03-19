import { BaseControlDescriptor } from './base-control.descriptor';

export interface TextDescriptor extends BaseControlDescriptor {
    editor?: 'ckeditor' | 'tiptap';
    config?: any;
}
