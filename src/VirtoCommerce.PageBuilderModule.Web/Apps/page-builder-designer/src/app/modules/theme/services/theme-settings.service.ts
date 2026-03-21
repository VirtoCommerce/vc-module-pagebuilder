import { inject, Injectable } from "@angular/core";
import { map, Observable } from 'rxjs';

import { BuilderHttpClient, AppConfig } from '@integration/services';
import { SettingsDataModel, SettingsSchemaModel } from '@theme/models';

@Injectable({
    providedIn: 'root'
})
export class ThemeSettingsService {

    private readonly http = inject(BuilderHttpClient);
    private readonly appConfig = inject(AppConfig);

    loadSettingsData(): Observable<SettingsDataModel | null> {
        const requestDescriptor = this.appConfig.getValue('settingsDataRequest');
        const request = this.http.generateRequest(requestDescriptor);
        return this.http.doRequest<SettingsDataModel>(request);
    }

    loadSettingsSchema(): Observable<SettingsSchemaModel | null> {
        const requestDescriptor = this.appConfig.getValue('settingsSchemaRequest')
        const request = this.http.generateRequest(requestDescriptor);
        return this.http.doRequest<SettingsSchemaModel>(request);
    }

    saveSettings(settings: SettingsDataModel): Observable<boolean> {
        const context = { item: JSON.stringify(settings), data: settings };
        const requestDescriptor = this.appConfig.getValue('saveSettings', context);
        const request = this.http.generateRequest(requestDescriptor, null, context);
        return this.http.doRequest(request).pipe(map(x => !!x));
    }
}
