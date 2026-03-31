import { Dictionary } from '@models/index';

export interface TemplateEntryState {
    id: string;
    isDirty: boolean;
}

export type TemplateEntryStateList = Dictionary<TemplateEntryState>;
