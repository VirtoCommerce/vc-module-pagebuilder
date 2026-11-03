import { Action, ActionReducer, combineReducers } from '@ngrx/store';

import { TemplateEditorDataEffects } from './data/effects';
import { TemplateEditorDomainEffects } from './domain/effects';
import { TemplateEditorUiEffects } from './ui/effects';
import { SharedComponentsDataEffects } from './shared-components/data/effects';
import { SharedComponentsDomainEffects } from './shared-components/domain/effects';
import { SharedComponentsUiEffects } from './shared-components/ui/effects';

import { EditorState } from './state';
import * as data from './data';
import * as ui from './ui';
import * as domain from './domain';

export { EditorFeatureName } from './state';

export const initialState: EditorState = {
  ui: ui.initialState,
  data: data.initialState,
  domain: domain.initialState,
};

const reducer: ActionReducer<EditorState> = combineReducers<EditorState>({
  ui: ui.editorUIReducers,
  data: data.editorDataReducers,
  domain: domain.editorDomainReducers,
});

export function editorReducers(state: EditorState = initialState, action: Action): EditorState {
  return reducer(state, action);
}

export const EFFECTS = [
  TemplateEditorDataEffects,
  TemplateEditorDomainEffects,
  TemplateEditorUiEffects,
  SharedComponentsDataEffects,
  SharedComponentsDomainEffects,
  SharedComponentsUiEffects,
];

export * from './state';
