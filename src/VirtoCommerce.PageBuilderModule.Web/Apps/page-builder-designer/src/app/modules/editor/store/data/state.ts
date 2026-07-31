import { LinkedComponentContentCache, LinkedComponentMetadataCache, SchemasList } from '@editor/models';
import { TemplateModelsList } from '@models/document';

export interface LinkedComponentsSearchState {
    keyword: string;
    resultIds: string[];
    optimisticResultIds: string[];
    loadedCount: number;
    totalCount: number;
    loading: boolean;
    rebasePending: boolean;
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
    linkedComponentUsageRefreshIdsByTemplate: Record<string, string[]>;
    linkedComponentDetails: LinkedComponentDetailsState;
    linkedComponentsSearch: LinkedComponentsSearchState;
}

export const initialState: EditorDataState = {
    templates: {},
    schemas: null,
    linkedComponents: {},
    linkedComponentContents: {},
    linkedComponentErrors: {},
    linkedComponentUsageRefreshIdsByTemplate: {},
    linkedComponentDetails: {
        componentId: null,
        loading: false,
        error: null,
    },
    linkedComponentsSearch: {
        keyword: '',
        resultIds: [],
        optimisticResultIds: [],
        loadedCount: 0,
        totalCount: 0,
        loading: false,
        rebasePending: false,
        error: null,
    },
}
