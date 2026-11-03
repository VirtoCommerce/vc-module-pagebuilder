import { Routes } from '@angular/router';
import { provideState } from '@ngrx/store';
import { provideEffects } from '@ngrx/effects';

import {
    ThemeEditorHostComponent,
    PresetsPanelComponent,
    ToolbarHostComponent
} from '@theme/components';
import { themeReducers, ThemeFeatureName, EFFECTS } from './store';

export const THEME_ROUTES: Routes = [
    {
        path: '',
        component: ThemeEditorHostComponent,
        providers: [
            provideState(ThemeFeatureName, themeReducers),
            provideEffects([...EFFECTS])
        ],
        data: { module: 'theme', toolbar: ToolbarHostComponent },
        children: [
            {
                path: 'presets',
                component: PresetsPanelComponent,
                data: { module: 'theme', mode: 'presets' }
            }
        ]
    }
];
