import { Action, ActionReducer, combineReducers } from '@ngrx/store';

import { ThemeDataEffects } from './data/effects';
import { ThemeDomainEffects } from './domain/effects';
import { ThemeUiEffects } from './ui/effects';

import { ThemeState } from './state';
import * as data from './data';
import * as ui from './ui';
import * as domain from './domain';

export { ThemeFeatureName } from './state';

export const initialState: ThemeState = {
  ui: ui.initialState,
  data: data.initialState,
  domain: domain.initialState
};

const reducer: ActionReducer<ThemeState> = combineReducers<ThemeState>({
  ui: ui.themeUIReducers,
  data: data.themeDataReducers,
  domain: domain.themeDomainReducers
});

export function themeReducers(
  state: ThemeState = initialState,
  action: Action
): ThemeState {
  return reducer(state, action);
}

export const EFFECTS = [ThemeDataEffects, ThemeDomainEffects, ThemeUiEffects];

export * from './state';
