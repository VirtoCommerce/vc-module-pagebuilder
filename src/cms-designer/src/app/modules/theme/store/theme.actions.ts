import { Action, createAction, props } from '@ngrx/store';
import { HttpErrorResponse } from '@angular/common/http';

import { PresetsModel } from '@themes/models';
import { BlockSchema, ValueType } from '@shared/models';

export const loadDefaultThemes = createAction('[Theme] Load Default Themes');
export const loadDefaultThemesSuccess = createAction('[Theme] Load Default Themes Success', props<{ presets: PresetsModel }>());
export const loadDefaultThemesFail = createAction('[Theme] Load Default Themes Fail', props<{ error: HttpErrorResponse }>());
export const loadEffectiveThemeValues = createAction('[Theme] Load Effective Theme Values',
    props<{ values: { [key: string]: ValueType } }>());
export const saveTheme = createAction('[Theme] Save Theme');
export const saveThemeSuccess = createAction('[Theme] Save Theme Success');
export const saveThemeFail = createAction('[Theme] Save Theme Fail', props<{ error: any }>());
export const loadSchema = createAction('[Theme] Load Schema');
export const loadSchemaSuccess = createAction('[Theme] Load Schema Success', props<{ schema: BlockSchema[] }>());
export const loadSchemaFail = createAction('[Theme] Load Schema Fail', props<{ error: HttpErrorResponse }>());
export const selectSchemaItem = createAction('[Theme] Select Schema Item', props<{ item: BlockSchema }>());
export const showPresetsPane = createAction('[Theme] Show Presets Pane');
export const cancelPreset = createAction('[Theme] Cancel preset');
export const applyPreset = createAction('[Theme] Apply preset', props<{ preset: string }>());
export const updateTheme = createAction('[Theme] Update Theme', props<{ values: { [key: string]: string | number | boolean } }>());
export const clearThemeChanges = createAction('[Theme] Clear Theme Changes');
export const removePreset = createAction('[Theme] Remove Preset', props<{ preset: string }>());
export const createPreset = createAction('[Theme] Create Preset', props<{ preset: string }>());
export const selectPreset = createAction('[Theme] Select Preset', props<{ preset: string }>());
export const updateDraft = createAction('[Theme] Update Draft');
export const updateDraftSuccess = createAction('[Theme] Update Draft Success');
export const updateDraftFail = createAction('[Theme] Update Draft Fail', props<{ error: any }>());
export const closeEditors = createAction('[Theme] Close Editors');
