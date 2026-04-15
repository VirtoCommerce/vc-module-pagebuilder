import { ServerRequestDescriptor } from '@models/index';
import { SectionPropertyDescriptor } from "@models/controls";
import { TemplateEntryList } from './template-entry-list.model';

export interface TemplateEntry {
    name: string;
    type?: string;
    path?: string;
    pageId?: string;
    request?: ServerRequestDescriptor | ServerRequestDescriptor[] | string | string[];
    sort?: number;
    key: string;
    prototype?: string;
    disabled?: boolean;
    previewUrl: string;
    previewRule: string;
    isDefault?: boolean | null;
    sections?: string[];
    settings?: SectionPropertyDescriptor[];
    children?: TemplateEntryList;
    hasChildren: boolean;
    previewMessage?: any;
    controls?: Record<string, any>;
}
