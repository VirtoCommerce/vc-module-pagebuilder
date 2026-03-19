import { Routes } from '@angular/router';
import { provideState } from '@ngrx/store';
import { provideEffects } from '@ngrx/effects';

import {
    TemplateEditorHostComponent,
    AddSectionComponent,
    EditSectionComponent,
    ToolbarHostComponent
} from '@editor/components';
import { EditorModuleInfo } from '@models/modules';
import { EditorFeatureName, editorReducers, EFFECTS } from './store';

export const EDITOR_ROUTES: Routes = [
    {
        path: '',
        component: TemplateEditorHostComponent,
        providers: [
            provideState(EditorFeatureName, editorReducers),
            provideEffects(EFFECTS)
        ],
        data: { module: EditorModuleInfo.name, toolbar: ToolbarHostComponent },
        children: [
            {
                path: 'add/:insertIndex',
                component: AddSectionComponent,
                data: { module: EditorModuleInfo.name, mode: 'add-section' }
            },
            {
                path: 'add/:sectionId/:insertIndex',
                component: AddSectionComponent,
                data: { module: EditorModuleInfo.name, mode: 'add-block' }
            },
            {
                path: 'settings',
                component: EditSectionComponent,
                data: { module: EditorModuleInfo.name, mode: EditorModuleInfo.mode.editSettings }
            },
            {
                path: 'settings/:settingsType',
                component: EditSectionComponent,
                data: { module: EditorModuleInfo.name, mode: EditorModuleInfo.mode.editSettings }
            },
            {
                path: ':sectionId',
                component: EditSectionComponent,
                data: { module: EditorModuleInfo.name, mode: 'edit-section' }
            },
            {
                path: ':sectionId/:blockId',
                component: EditSectionComponent,
                data: { module: EditorModuleInfo.name, mode: 'edit-block' }
            }
        ]
    }
];
