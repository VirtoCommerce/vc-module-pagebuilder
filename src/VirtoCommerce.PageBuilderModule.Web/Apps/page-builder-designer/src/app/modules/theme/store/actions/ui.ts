import { createAction, props } from '@ngrx/store';

export const presetsListMode = createAction('[theme ui] presets list mode');
export const presetsTileMode = createAction('[theme ui] presets tile mode');
export const applyPresetsFilter = createAction('[theme ui] apply presets filter', props<{ filter: string }>());
