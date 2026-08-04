export * from './paste-content/paste-content.component';
export * from './shared-component-name/shared-component-name.component';
export * from './shared-component-insert-mode/shared-component-insert-mode.component';

import { PasteContentComponent } from './paste-content/paste-content.component';
import { SharedComponentNameComponent } from './shared-component-name/shared-component-name.component';
import { SharedComponentInsertModeComponent } from './shared-component-insert-mode/shared-component-insert-mode.component';

export const DIALOGS = [
    PasteContentComponent,
    SharedComponentNameComponent,
    SharedComponentInsertModeComponent,
];
