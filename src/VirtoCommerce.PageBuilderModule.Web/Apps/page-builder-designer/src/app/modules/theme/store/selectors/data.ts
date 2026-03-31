import { createSelector } from "@ngrx/store";

import { selectThemeDataState, selectPresetsFilter } from "./common";

export const selectCurrentSettings = createSelector(
  selectThemeDataState,
  state => state.settings
);

const selectSourceSettings = createSelector(
  selectThemeDataState,
  state => state.sourceSettings
);

export const selectPresets = createSelector(
  selectThemeDataState,
  state => state.sourceSettings?.presets
);

export const selectFilteredPresets = createSelector(
  selectPresets,
  selectPresetsFilter,
  (presets, filter) => !filter || !presets
    ? presets || []
    : Object.keys(presets)
      .filter(key => key.toLowerCase().includes(filter?.toLowerCase()))
      .reduce((result, key) => ({ ...result, [key]: presets[key] }), {})
);

export const selectPresetsNames = createSelector(
  selectPresets,
  presets => presets ? Object.keys(presets) : []
);

export const selectSettingsSchema = createSelector(
  selectThemeDataState,
  selectCurrentSettings,
  (state, currentSettings) => currentSettings ? state.settingsSchema : null
);

export const selectCurrentSettingsDataModel = createSelector(
  selectSourceSettings,
  selectCurrentSettings,
  (sourceSettings, current) => ({
    ...sourceSettings!,
    current: current!
  })
);
