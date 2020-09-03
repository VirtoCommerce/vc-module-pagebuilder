import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { PresetsModel } from '@themes/models';
import { PlatformService } from '@app/services';
import { BlockSchema, ValueType } from '@shared/models';

@Injectable({
    providedIn: 'root'
})
export class ThemeService {

    constructor(private platform: PlatformService) { }

    loadPresets(): Observable<{ presets: PresetsModel, basePresets: PresetsModel}> {
        return this.platform.downloadSettingsData();
    }

    loadSchema(): Observable<BlockSchema[]> {
        return this.platform.downloadSettingsSchema();
    }

    uploadPresets(model: { [key: string]: ValueType }, presets: PresetsModel, basePresets: PresetsModel, schema: BlockSchema[]): Observable<any> {
        return this.platform.uploadPreset(model, presets, basePresets, schema);
    }

    uploadDraft(model: { [key: string]: ValueType }): Observable<any> {
        return this.platform.uploadDraftPreset(model);
    }
}
