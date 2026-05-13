export * from './confirm/confirm.component';
export * from './alert/alert.component';
export * from './asset-picker/asset-picker.component';

import { ConfirmComponent } from './confirm/confirm.component';
import { AlertComponent } from './alert/alert.component';
import { AssetPickerComponent } from './asset-picker/asset-picker.component';

export const DIALOGS = [
    ConfirmComponent,
    AlertComponent,
    AssetPickerComponent
];
