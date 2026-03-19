import { createSelector } from "@ngrx/store";

import { selectGroupsParameter, selectPresetParameter } from '@shared/routing'
import { selectThemeDomainState } from './common';
import { selectSettingsSchema, selectPresetsNames } from "./data";

export const selectOpenedGroups = createSelector(
    selectGroupsParameter,
    group => group ? <string[]>group.split(',') : []
);

export const selectEditableGroup = createSelector(
    selectOpenedGroups,
    selectSettingsSchema,
    (groups, schema) => schema?.filter(x => x.inline === false).find(x => groups.indexOf(x.name) !== -1)
);

export const selectPresetsState = createSelector(
    selectPresetsNames,
    selectPresetParameter,
    (presets, preset) => presets ? presets.reduce((acc, cur) => ({ ...acc, [cur]: { current: cur === preset } }), {}) : <any>{}
);

export const selectIsDirty = createSelector(
    selectThemeDomainState,
    state => state.isDirty
);
