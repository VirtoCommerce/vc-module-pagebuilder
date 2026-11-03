export * from './confirm/confirm.component';
export * from './alert/alert.component';
export * from './asset-picker/asset-picker.component';
export * from './asset-overwrite/asset-overwrite.component';

import { ConfirmComponent } from './confirm/confirm.component';
import { AlertComponent } from './alert/alert.component';
import { AssetPickerComponent } from './asset-picker/asset-picker.component';
import { AssetOverwriteComponent } from './asset-overwrite/asset-overwrite.component';

export const DIALOGS = [
    ConfirmComponent,
    AlertComponent,
    AssetPickerComponent,
    AssetOverwriteComponent
];
