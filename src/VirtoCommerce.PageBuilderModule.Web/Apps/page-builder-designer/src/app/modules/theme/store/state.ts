import { BuilderState as ParentState } from '@shared/store'

import * as data from './data';
import * as ui from './ui';
import * as domain from './domain';

export interface ThemeState {
    ui: ui.ThemeUIState;
    data: data.ThemeDataState;
    domain: domain.ThemeDomainState
};

export const ThemeFeatureName = 'themeEditor';

export interface BuilderState extends ParentState {
    [ThemeFeatureName]: ThemeState
}
