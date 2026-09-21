import { SharedComponentContentCache, SharedComponentMetadataCache, SchemasList } from '@editor/models';
import { TemplateModelsList } from '@models/document';

export interface SharedComponentsSearchState {
    keyword: string;
    resultIds: string[];
    optimisticResultIds: string[];
    loadedCount: number;
    totalCount: number;
    loading: boolean;
    rebasePending: boolean;
    error: string | null;
}

export interface SharedComponentDetailsState {
    componentId: string | null;
    loading: boolean;
    error: string | null;
}

export interface EditorDataState {
    templates: TemplateModelsList;
    schemas: SchemasList | null;
    sharedComponents: SharedComponentMetadataCache;
    sharedComponentContents: SharedComponentContentCache;
    sharedComponentErrors: Record<string, string>;
    sharedComponentUsageRefreshIdsByTemplate: Record<string, string[]>;
    sharedComponentDetails: SharedComponentDetailsState;
    sharedComponentsSearch: SharedComponentsSearchState;
}

export const initialState: EditorDataState = {
    templates: {},
    schemas: null,
    sharedComponents: {},
    sharedComponentContents: {},
    sharedComponentErrors: {},
    sharedComponentUsageRefreshIdsByTemplate: {},
    sharedComponentDetails: {
        componentId: null,
        loading: false,
        error: null,
    },
    sharedComponentsSearch: {
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
