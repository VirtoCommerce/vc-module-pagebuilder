import { Action, createAction, props } from '@ngrx/store';

export const emptyAction = createAction('[Root] Empty Action');
export const resetData = createAction('[Root] Reset Data');
export const saveData = createAction('[Root] Save Data');
export const loadData = createAction('[Root] Load Data');
export const previewLoading = createAction('[Root] Preview Loading', props<{isLoading: boolean, msg: string}>());
export const previewReady = createAction('[Root] Preview is Ready', props<{frameId: string}>());
export const previewError = createAction('[Root] Preview in Error mode', props<{error: any}>());
export const reloadPreview = createAction('[Root] Reload Preview');
export const checkPreviewLoadedOrError = createAction('[Root] Check Preview Loaded Or Error');
export const toggleFrames = createAction('[Root] Toggle Frames', props<{frameId: string}>());
export const closeEditors = createAction('[Root] Close editors');
export const tabIndexChanged = createAction('[Root] Tab Index Changed', props<{tabIndex: number}>());
export const setPreviewUrl = createAction('[Root] Set Preview Url', props<{url: string}>());
