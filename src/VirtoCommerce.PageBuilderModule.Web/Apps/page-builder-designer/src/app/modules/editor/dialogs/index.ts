export * from './paste-content/paste-content.component';
export * from './linked-component-name/linked-component-name.component';
export * from './linked-component-insert-mode/linked-component-insert-mode.component';

import { PasteContentComponent } from './paste-content/paste-content.component';
import { LinkedComponentNameComponent } from './linked-component-name/linked-component-name.component';
import { LinkedComponentInsertModeComponent } from './linked-component-insert-mode/linked-component-insert-mode.component';

export const DIALOGS = [
    PasteContentComponent,
    LinkedComponentNameComponent,
    LinkedComponentInsertModeComponent,
];
