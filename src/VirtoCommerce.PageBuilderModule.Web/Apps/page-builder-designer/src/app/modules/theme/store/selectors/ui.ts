import { createSelector } from "@ngrx/store";

import { ActionButtonDescriptor } from '@core/models';

import { selectThemeUIState } from "./common";
import { selectSettingsSchema, selectFilteredPresets } from "./data";
import { selectOpenedGroups, selectPresetsState, selectIsDirty } from "./domain";

export const selectGroupsState = createSelector(
    selectSettingsSchema,
    selectOpenedGroups,
    (schema, groups) => schema?.reduce((result, current) => {
        result[current.name] = {
            opened: groups.indexOf(current.name) !== -1 && current.inline !== false,
        };
        return result;
    }, <any>{})
);

export const isLoading = createSelector(
    selectThemeUIState,
    state => !!(state?.settingsLoading || state?.schemaLoading)
);

export const selectPresetsContext = createSelector(
    selectFilteredPresets,
    selectPresetsState,
    (presets, state) => ({ presets, state })
);

export const selectToolbarButtonsState = createSelector(
    selectIsDirty,
    isDirty => (<ActionButtonDescriptor[][]>[
            // [
            //     {
            //         canAction: false,
            //         icon: 'undo',
            //         alias: 'undo'
            //     },
            //     {
            //         canAction: false,
            //         icon: 'redo',
            //         alias: 'redo'
            //     }
            // ],
            [
                {
                    title: 'Cancel',
                    alias: 'cancel',
                    type: 'secondary'
                },
                {
                    canAction: isDirty,
                    title: 'Save settings',
                    alias: 'save',
                    type: 'primary'
                }
            ]
        ]
    )
);
