import { createAction, props } from "@ngrx/store";
import { ModelChangedEventArgs } from "@core/models";
import { PresetsModel } from '@theme/models';

export const applyPreset = createAction('[theme presets] apply preset', props<{ preset: string }>());

export const toggleGroup = createAction('[theme settings] toggle group', props<{ group: any }>());
export const gotoPresets = createAction('[theme settings] go to presets');
export const previewPreset = createAction('[theme settings] preview preset', props<{ preset: string }>());
export const exitPresets = createAction('[theme settings] exit presets');
export const exitSettings = createAction('[theme settings] exit settings');

export const updateSettings = createAction('[theme settings] update settings', props<ModelChangedEventArgs>());
export const updateInPreview = createAction('[theme settings] update in preview', props<{ settings: PresetsModel | null }>());
