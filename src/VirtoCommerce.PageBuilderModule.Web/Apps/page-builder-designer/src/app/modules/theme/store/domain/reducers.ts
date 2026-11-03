import { createReducer, on } from '@ngrx/store';

import * as actions from '../actions';

import { ThemeDomainState, initialState } from './state';

export const themeDomainReducers = createReducer<ThemeDomainState>(
    initialState,

    on(actions.updateSettings, actions.applyPreset, (state) => ({ ...state, isDirty: true })),
    on(actions.revertChanges, actions.applyChanges, (state) => ({ ...state, isDirty: false }))
);
