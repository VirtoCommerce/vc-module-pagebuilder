import { BuilderState as ParentState } from '@shared/store'

import * as data from './data';
import * as ui from './ui';
import * as domain from './domain';

export interface EditorState {
  ui: ui.EditorUIState;
  data: data.EditorDataState;
  domain: domain.EditorDomainState;
}

export const EditorFeatureName = 'templateEditor';

export interface BuilderState extends ParentState {
  [EditorFeatureName]: EditorState;
}
