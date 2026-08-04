import { createAction, props } from '@ngrx/store';

import { TemplateModel } from '@models/document';
import { SharedComponent, SharedComponentSearchResult } from '@editor/models';
import { PreviewOutboundMessage } from '@shared/models';

export const broadcastResolvedPreview = createAction(
    '[shared components] broadcast resolved preview',
    props<{ msg: PreviewOutboundMessage }>(),
);

export const cacheSharedComponent = createAction(
    '[shared components] cache component',
    props<{ component: SharedComponent; content?: TemplateModel; addToSearchResults?: boolean }>(),
);

export const cacheSharedComponentContent = createAction(
    '[shared components] cache component content',
    props<{ componentId: string; content: TemplateModel }>(),
);

export const clearSharedComponentUsageRefresh = createAction(
    '[shared components] clear pending usage refresh',
    props<{ templateKey: string }>(),
);

export const loadSharedComponentDetails = createAction(
    '[shared components] load component details',
    props<{ componentId: string }>(),
);

export const loadSharedComponentDetailsSuccess = createAction(
    '[shared components] load component details success',
    props<{ component: SharedComponent }>(),
);

export const loadSharedComponentDetailsFailed = createAction(
    '[shared components] load component details failed',
    props<{ componentId: string; error: string }>(),
);

export const clearSharedComponentDetails = createAction(
    '[shared components] clear component details',
);

export const sharedComponentLoadFailed = createAction(
    '[shared components] component load failed',
    props<{ componentId: string; error: string }>(),
);

export const searchSharedComponents = createAction(
    '[shared components] search',
    props<{ keyword: string; skip?: number }>(),
);

export const retrySharedComponentsSearch = createAction(
    '[shared components] retry search',
    props<{ keyword: string; skip?: number }>(),
);

export const refreshSharedComponentsSearch = createAction(
    '[shared components] refresh current search',
    props<{ keyword: string }>(),
);

export const searchSharedComponentsSuccess = createAction(
    '[shared components] search success',
    props<{ keyword: string; result: SharedComponentSearchResult; append?: boolean; rebase?: boolean }>(),
);

export const searchSharedComponentsFailed = createAction(
    '[shared components] search failed',
    props<{ keyword: string; error: string }>(),
);

export const insertSharedComponent = createAction(
    '[shared components] insert',
    props<{ componentId: string; mode: 'shared' | 'copy'; insertIndex: number }>(),
);

export const chooseSharedComponentInsertionMode = createAction(
    '[shared components] choose insertion mode',
    props<{
        componentId: string;
        insertIndex: number;
        defaultMode: 'shared' | 'copy';
    }>(),
);

export const saveSelectionAsSharedComponent = createAction(
    '[shared components] save selected sections',
);

export const detachSharedComponent = createAction(
    '[shared components] detach',
    props<{ sectionId: string; componentId: string }>(),
);

export const openSharedComponent = createAction(
    '[shared components] open document',
    props<{ componentId: string }>(),
);

export const closeSharedComponent = createAction(
    '[shared components] close document',
);

export const discardSharedComponentChanges = createAction(
    '[shared components] discard document changes',
    props<{ templateKey: string }>(),
);

export const openSharedComponentUsagePage = createAction(
    '[shared components] open usage page',
    props<{ pageId: string; cultureName: string | null | undefined }>(),
);
