import { TemplateStatesList } from '@editor/models';

export interface EditorDomainState {
    states: TemplateStatesList;
    schemaLoading: boolean;
}

export const initialState: EditorDomainState = {
    states: {},
    schemaLoading: false
}
