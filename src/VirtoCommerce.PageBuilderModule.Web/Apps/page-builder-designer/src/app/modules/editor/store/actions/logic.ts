import { createAction, props } from "@ngrx/store";
import {
    ClipboardModel,
    ReorderItemsModel,
} from "@core/models";

import {
    SectionModel,
    SectionSchema
} from "@models/document";

export const showBlankSections = createAction('[template editor] show blank sections', props<{ sectionId: string | null, positionIndex: number }>());
export const closeAddItemPanel = createAction('[template editor] close add item panel');
export const editSectionAction = createAction('[template editor] edit section', props<{ sectionId: string }>());
export const editBlockAction = createAction('[template editor] edit block', props<{ sectionId: string, blockId: string }>());
export const closeEditItemPanel = createAction('[template editor] close edit item panel');
export const sortItems = createAction('[template editor] sort items', props<{ options: ReorderItemsModel }>());
export const pasteFromClipboard = createAction('[template editor] paste from clipboard', props<{ value: ClipboardModel, section?: SectionModel, block?: SectionModel, action: string, source: 'list' | 'editor' }>());
export const showClipboardModal = createAction('[template editor] showClipboardModal', props<{ value: ClipboardModel, section?: SectionModel, block?: SectionModel, action: string, source: 'list' | 'editor' }>());

export const hoverSection = createAction('[template editor] hover section', props<{ sectionId: string | null }>());

export const templateContentChanged = createAction('[template editor] template content changed');

export const addItemAction = createAction('[template editor] add item', props<{ schema: SectionSchema }>());

export const editSettings = createAction('[template editor] edit settings', props<{ schema: SectionSchema }>());

export const sectionChangedAction = createAction('[template editor] section changed', props<{ changes: Partial<SectionModel> }>());
export const executeContextMenuAction = createAction('[template editor] execute context menu action', props<{ action: string, source: 'list' | 'editor', section?: SectionModel, block?: SectionModel }>());

export const executeToolbarAction = createAction('[template editor] execute toolbar action', props<{ action: string }>());
export const refreshPreview = createAction('[template editor] refresh preview');
