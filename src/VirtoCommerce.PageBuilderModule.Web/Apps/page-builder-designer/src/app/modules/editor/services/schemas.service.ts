import { Injectable, inject } from "@angular/core";
import { Observable } from 'rxjs';

import { AppConfig, BuilderHttpClient } from '@integration/services';
import { SchemasList } from '@editor/models';

@Injectable({
    providedIn: 'root'
})
export class SchemasService {

    private readonly http = inject(BuilderHttpClient);
    private readonly appConfig = inject(AppConfig);

    getSchemas(): Observable<SchemasList | null> {
        const sectionsListUrl = this.appConfig.getValue('sectionsListUrl');
        const request = this.http.generateRequest(sectionsListUrl);
        return this.http.doRequest<SchemasList>(request);
    }
}
