import { HttpErrorResponse } from '@angular/common/http';
import { createAction, props } from '@ngrx/store';
import { SettingsDataModel, SettingsSchemaModel } from '@theme/models';

export const presetsListMode = createAction('[theme ui] presets list mode');
export const presetsTileMode = createAction('[theme ui] presets tile mode');
export const applyPresetsFilter = createAction('[theme ui] apply presets filter', props<{ filter: string }>());
