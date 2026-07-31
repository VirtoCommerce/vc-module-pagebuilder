import { createAction, props } from '@ngrx/store';

import { TemplateModel } from '@models/document';
import { LinkedComponent, LinkedComponentSearchResult } from '@editor/models';
import { PreviewOutboundMessage } from '@shared/models';

export const broadcastResolvedPreview = createAction(
    '[linked components] broadcast resolved preview',
    props<{ msg: PreviewOutboundMessage }>(),
);

export const cacheLinkedComponent = createAction(
    '[linked components] cache component',
    props<{ component: LinkedComponent; content?: TemplateModel; addToSearchResults?: boolean }>(),
);

export const cacheLinkedComponentContent = createAction(
    '[linked components] cache component content',
    props<{ componentId: string; content: TemplateModel }>(),
);

export const clearLinkedComponentUsageRefresh = createAction(
    '[linked components] clear pending usage refresh',
    props<{ templateKey: string }>(),
);

export const loadLinkedComponentDetails = createAction(
    '[linked components] load component details',
    props<{ componentId: string }>(),
);

export const loadLinkedComponentDetailsSuccess = createAction(
    '[linked components] load component details success',
    props<{ component: LinkedComponent }>(),
);

export const loadLinkedComponentDetailsFailed = createAction(
    '[linked components] load component details failed',
    props<{ componentId: string; error: string }>(),
);

export const clearLinkedComponentDetails = createAction(
    '[linked components] clear component details',
);

export const linkedComponentLoadFailed = createAction(
    '[linked components] component load failed',
    props<{ componentId: string; error: string }>(),
);

export const searchLinkedComponents = createAction(
    '[linked components] search',
    props<{ keyword: string; skip?: number }>(),
);

export const retryLinkedComponentsSearch = createAction(
    '[linked components] retry search',
    props<{ keyword: string; skip?: number }>(),
);

export const refreshLinkedComponentsSearch = createAction(
    '[linked components] refresh current search',
    props<{ keyword: string }>(),
);

export const searchLinkedComponentsSuccess = createAction(
    '[linked components] search success',
    props<{ keyword: string; result: LinkedComponentSearchResult; append?: boolean; rebase?: boolean }>(),
);

export const searchLinkedComponentsFailed = createAction(
    '[linked components] search failed',
    props<{ keyword: string; error: string }>(),
);

export const insertLinkedComponent = createAction(
    '[linked components] insert',
    props<{ componentId: string; mode: 'linked' | 'copy'; insertIndex: number }>(),
);

export const chooseLinkedComponentInsertionMode = createAction(
    '[linked components] choose insertion mode',
    props<{
        componentId: string;
        insertIndex: number;
        defaultMode: 'linked' | 'copy';
    }>(),
);

export const saveSelectionAsLinkedComponent = createAction(
    '[linked components] save selected sections',
);

export const detachLinkedComponent = createAction(
    '[linked components] detach',
    props<{ sectionId: string; componentId: string }>(),
);

export const openLinkedComponent = createAction(
    '[linked components] open document',
    props<{ componentId: string }>(),
);

export const closeLinkedComponent = createAction(
    '[linked components] close document',
);

export const discardLinkedComponentChanges = createAction(
    '[linked components] discard document changes',
    props<{ templateKey: string }>(),
);

export const openLinkedComponentUsagePage = createAction(
    '[linked components] open usage page',
    props<{ pageId: string; cultureName: string | null | undefined }>(),
);
