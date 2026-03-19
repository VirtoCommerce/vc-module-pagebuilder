export * from './base-control.descriptor';
export * from './calendar.descriptor';
export * from './checkbox.descriptor';
export * from './collection.descriptor';
export * from './color.descriptor';
export * from './display-text.descriptor';
export * from './files.descriptor';
export * from './images.descriptor';
export * from './number.descriptor';
export * from './object.descriptor';
export * from './search.descriptor';
export * from './select.descriptor';
export * from './select-option.model';
export * from './string.descriptor';
export * from './text.descriptor';
export * from './markdown.descriptor';
export * from './upload-asset.descriptor';

// todo: list, object, search, popup (list|object), url? (maybe should be object)

import { CalendarDescriptor } from './calendar.descriptor';
import { CheckboxDescriptor } from './checkbox.descriptor';
import { CollectionDescriptor } from './collection.descriptor';
import { ColorDescriptor } from './color.descriptor';
import { DisplayTextDescriptor } from './display-text.descriptor';
import { FilesDescriptor } from './files.descriptor';
import { ImagesDescriptor } from './images.descriptor';
import { NumberDescriptor } from './number.descriptor';
import { ObjectDescriptor } from './object.descriptor';
import { SearchDescriptor } from './search.descriptor';
import { SelectDescriptor } from './select.descriptor';
import { StringDescriptor } from './string.descriptor';
import { TextDescriptor } from './text.descriptor';
import { MarkdownDescriptor } from './markdown.descriptor';

export type ControlDescriptor = CalendarDescriptor
    | CheckboxDescriptor
    | CollectionDescriptor
    | ColorDescriptor
    | DisplayTextDescriptor
    | FilesDescriptor
    | ImagesDescriptor
    | NumberDescriptor
    | ObjectDescriptor
    | SearchDescriptor
    | SelectDescriptor
    | StringDescriptor
    | TextDescriptor
    | MarkdownDescriptor;

export type SectionPropertyDescriptor = ControlDescriptor;
