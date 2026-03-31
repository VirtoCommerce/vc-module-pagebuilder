import { TemplateEntryState } from './template-entry-state.model';
import { TemplateEntry } from "./template-entry.model";

export interface TemplateEntryInfo {
    key: string;
    parent?: string;
    name: string;
    entry: TemplateEntry;
    state: TemplateEntryState;
}
