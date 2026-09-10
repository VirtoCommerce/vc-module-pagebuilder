import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { AppConfig, BuilderHttpClient } from '@integration/services';
import { TemplateModel } from '@models/document';

import {
    CreateSharedComponentRequest,
    SharedComponent,
    SharedComponentSearchResult,
} from '@editor/models/shared-component.model';

export const SHARED_COMPONENTS_PAGE_SIZE = 20;

@Injectable({ providedIn: 'root' })
export class SharedComponentsService {
    private readonly http = inject(BuilderHttpClient);
    private readonly appConfig = inject(AppConfig);
    private readonly apiUrl = '/api/page-builder-shared-components';

    get storeId(): string {
        return this.appConfig.getContext().location.params['storeId'] ?? '';
    }

    search(keyword = '', skip = 0, take = SHARED_COMPONENTS_PAGE_SIZE): Observable<SharedComponentSearchResult> {
        return this.http.post<SharedComponentSearchResult>(this.apiUrl + '/search', {
            storeId: this.storeId,
            keyword: keyword.trim(),
            skip,
            take,
        });
    }

    create(name: string, content: TemplateModel): Observable<SharedComponent> {
        const request: CreateSharedComponentRequest = {
            storeId: this.storeId,
            name: name.trim(),
            content,
        };
        return this.http.post<SharedComponent>(this.apiUrl, request);
    }

    get(id: string): Observable<SharedComponent> {
        return this.http.get<SharedComponent>(`${this.apiUrl}/${encodeURIComponent(id)}`);
    }

    getContent(id: string): Observable<TemplateModel> {
        return this.http.get<TemplateModel>(`${this.apiUrl}/${encodeURIComponent(id)}/content`);
    }

    updateContent(id: string, content: TemplateModel): Observable<void> {
        return this.http.put<void>(`${this.apiUrl}/${encodeURIComponent(id)}/content`, content);
    }
}
