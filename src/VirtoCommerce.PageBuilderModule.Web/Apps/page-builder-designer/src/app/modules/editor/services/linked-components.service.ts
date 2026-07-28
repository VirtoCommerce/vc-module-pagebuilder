import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { AppConfig, BuilderHttpClient } from '@integration/services';
import { TemplateModel } from '@models/document';

import {
    CreateLinkedComponentRequest,
    LinkedComponent,
    LinkedComponentSearchResult,
    UpdateLinkedComponentRequest,
} from '@editor/models/linked-component.model';

export const LINKED_COMPONENTS_PAGE_SIZE = 20;

@Injectable({ providedIn: 'root' })
export class LinkedComponentsService {
    private readonly http = inject(BuilderHttpClient);
    private readonly appConfig = inject(AppConfig);
    private readonly apiUrl = '/api/page-builder-linked-components';

    get storeId(): string {
        return this.appConfig.getContext().location.params['storeId'] ?? '';
    }

    search(keyword = '', skip = 0, take = LINKED_COMPONENTS_PAGE_SIZE): Observable<LinkedComponentSearchResult> {
        return this.http.post<LinkedComponentSearchResult>(this.apiUrl + '/search', {
            storeId: this.storeId,
            keyword: keyword.trim(),
            skip,
            take,
        });
    }

    create(name: string, content: TemplateModel): Observable<LinkedComponent> {
        const request: CreateLinkedComponentRequest = {
            storeId: this.storeId,
            name: name.trim(),
            content,
        };
        return this.http.post<LinkedComponent>(this.apiUrl, request);
    }

    get(id: string): Observable<LinkedComponent> {
        return this.http.get<LinkedComponent>(`${this.apiUrl}/${encodeURIComponent(id)}`);
    }

    update(id: string, name: string): Observable<LinkedComponent> {
        const request: UpdateLinkedComponentRequest = { storeId: this.storeId, name: name.trim() };
        return this.http.put<LinkedComponent>(`${this.apiUrl}/${encodeURIComponent(id)}`, request);
    }

    delete(id: string): Observable<void> {
        return this.http.delete<void>(`${this.apiUrl}/${encodeURIComponent(id)}`);
    }

    getContent(id: string): Observable<TemplateModel> {
        return this.http.get<TemplateModel>(`${this.apiUrl}/${encodeURIComponent(id)}/content`);
    }

    updateContent(id: string, content: TemplateModel): Observable<void> {
        return this.http.put<void>(`${this.apiUrl}/${encodeURIComponent(id)}/content`, content);
    }
}
