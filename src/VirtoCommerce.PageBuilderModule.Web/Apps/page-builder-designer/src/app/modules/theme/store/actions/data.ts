import { HttpErrorResponse } from '@angular/common/http';
import { createAction, props } from '@ngrx/store';
import { SettingsDataModel, SettingsSchemaModel } from '@theme/models';

export const raiseLoadData = createAction('[theme data] raise load data');

export const loadSettingsData = createAction('[theme data] load settings data');
export const loadSettingsDataSuccess = createAction('[theme data] load settings data success', props<{ settingsData: SettingsDataModel | null }>());
export const loadSettingsDataFail = createAction('[theme data] load settings data fail', props<{ error: HttpErrorResponse }>());

export const loadSettingsSchema = createAction('[theme data] load settings schema');
export const loadSettingsSchemaSuccess = createAction('[theme data] load settings schema success', props<{ schema: SettingsSchemaModel | null }>());
export const loadSettingsSchemaFail = createAction('[theme data] load settings schema fail', props<{ error: HttpErrorResponse }>());

export const useSettingsSchema = createAction('[theme data] use settings schema', props<{ schema: SettingsSchemaModel | null }>());

export const saveSettings = createAction('[theme data] save settings');
export const saveSettingsSuccess = createAction('[theme data] save settings success');
export const saveSettingsFail = createAction('[theme data] save settings fail', props<{ error: HttpErrorResponse }>());

export const executeAction = createAction('[theme data] execute action', props<{ action: string }>());

export const revertChanges = createAction('[theme data] revert changes');
export const applyChanges = createAction('[theme data] apply changes');
