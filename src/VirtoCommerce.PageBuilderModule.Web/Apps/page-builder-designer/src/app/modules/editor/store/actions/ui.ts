import { createAction, props } from "@ngrx/store";
import { SectionSchema } from '@models/document';
import { SectionState } from "@editor/models";

export const sectionStateChangedAction = createAction('[template editor] section state changed section', props<{ sectionId: string, templateKey: string, state: Partial<SectionState> }>());
export const toggleGroupAction = createAction('[template editor] toggle group', props<{ groupId: string }>());
export const previewItemAction = createAction('[template editor] preview item', props<{ item: SectionSchema }>());
export const applySectionsFilter = createAction('[template editor] apply sections filter', props<{ filter: string | null }>());
export const resetGroupsState = createAction('[template editor] reset groups state');

export const startDragSection = createAction('[template editor] start drag section', props<{ sectionId: string }>());
export const releaseDragSection = createAction('[template editor] release drag section', props<{ sectionId: string }>());
export const setWindowTitle = createAction('[template editor] set window title');
