import { LinkedComponentContentCache, LinkedComponentMetadataCache, SchemasList } from '@editor/models';
import { TemplateModelsList } from '@models/document';

export interface LinkedComponentsSearchState {
    keyword: string;
    resultIds: string[];
    totalCount: number;
    loading: boolean;
    error: string | null;
}

export interface LinkedComponentDetailsState {
    componentId: string | null;
    loading: boolean;
    error: string | null;
}

export interface EditorDataState {
    templates: TemplateModelsList;
    schemas: SchemasList | null;
    linkedComponents: LinkedComponentMetadataCache;
    linkedComponentContents: LinkedComponentContentCache;
    linkedComponentErrors: Record<string, string>;
    linkedComponentDetails: LinkedComponentDetailsState;
    linkedComponentsSearch: LinkedComponentsSearchState;
}

export const initialState: EditorDataState = {
    templates: {},
    schemas: null,
    linkedComponents: {},
    linkedComponentContents: {},
    linkedComponentErrors: {},
    linkedComponentDetails: {
        componentId: null,
        loading: false,
        error: null,
    },
    linkedComponentsSearch: {
        keyword: '',
        resultIds: [],
        totalCount: 0,
        loading: false,
        error: null,
    },
}
