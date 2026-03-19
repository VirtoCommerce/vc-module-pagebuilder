import { HttpErrorResponse } from "@angular/common/http";
import { createAction, props } from "@ngrx/store";

import { EventBusArgs } from "@core/models";
import { TemplateEntryList } from '@shared/models';

export const initShared = createAction('[shared] init');
export const initApp = createAction('[app] init');
export const empty = createAction('[app] empty');

// todo: i think that exists the more flexible solution than next action
export const setLivePreviewUrl = createAction('[shared] set live preview url');

export const setHttpLoading = createAction('[shared] set http loading', props<{ isLoading: boolean }>());

export const loadTemplateEntries = createAction('[shared] load template entries');
export const loadTemplateEntriesSuccess = createAction('[shared] load template entries success', props<{ templatesEntries: TemplateEntryList }>());
export const loadTemplateEntriesFails = createAction('[shared] load template entries fails', props<{ error: HttpErrorResponse }>());

export const useTemplateEntries = createAction('[shared] use template entries', props<{ templatesEntries: TemplateEntryList }>());

export const templateChanged = createAction('[shared] template changed', props<{ templateType: string, path?: string, pageId?: string, parent: string | null }>());
export const selectDefaultTemplate = createAction('[shared] select first template');
export const changePreviewMode = createAction('[shared] change preview mode', props<{ mode: string | null }>());

// export const setCurrentDirtyState = createAction('[shared] set current dirty state', props<{ dirty: boolean }>());
export const setRootDirtyState = createAction('[shared] set root dirty state', props<{ templateKey: string, dirty: boolean }>());
export const setDirtyState = createAction('[shared] set dirty state', props<{ templateKey: string, parentKey?: string, dirty: boolean }>());

export const selectTemplate = createAction('[shared] select template', props<{ templateType: string, path?: string, pageId?: string, templateKey: string }>());
export const navigateToCurrentTemplate = createAction('[shared] navigate to current template');
export const filterTemplates = createAction('[shared] filter templates', props<{ filter: string }>());
export const displayRootTemplates = createAction('[shared] display root templates');

export const switchToChildrenTemplates = createAction('[shared] switch to children templates', props<{ templateKey: string }>());
export const raiseLoadChildrenTemplates = createAction('[shared] raise load children templates');
export const loadChildrenTemplates = createAction('[shared] load children templates', props<{ templateKey: string, onInit: boolean }>());
export const loadChildrenTemplatesSuccess = createAction('[shared] load children templates success', props<{ childrenEntries: TemplateEntryList, parentTemplate: string }>());
export const loadChildrenTemplatesFails = createAction('[shared] load children requested', props<{ error: HttpErrorResponse, parentTemplate: string }>());

export const broadcastPreviewMessage = createAction('[shared] broadcast preview message', props<{ msg: any }>());
export const broadcastPlatformMessage = createAction('[shared] broadcast platform message', props<{ msg: any }>());
export const showNotification = createAction('[shared] show notification', props<{ message: string, msgType: 'error'|'success'|'info'|'warning', top?: boolean }>());

export const previewLoaded = createAction('[shared] preview loaded');
export const selectSection = createAction('[shared] select section', props<{ sectionId: number }>());
export const setWindowTitle = createAction('[shared] set window title', props<{ title: string | null }>());
export const previewSectionHovered = createAction('[shared] preview section hovered', props<{ sectionId: string | null }>());

export const updateCustomSchemas = createAction('[shared] update custom schemas', props<{ schemas: any }>());
